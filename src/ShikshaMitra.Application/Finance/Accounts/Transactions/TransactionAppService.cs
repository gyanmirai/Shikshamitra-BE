using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Transactions;

[RemoteService(false)]
public partial class TransactionAppService(
	IRepository<Transaction, Guid> transactionRepository,
	IRepository<TransactionCategory, Guid> transactionCategoryRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	ITransactionCategoryReferenceLock referenceLock,
	FinanceScopeAccessService financeScopeAccessService,
    FinanceRoutingManager routingManager,
    IRepository<ShikshaMitra.TenantSettings.AcademicYears.AcademicYear, Guid> academicYears
) : ApplicationService, ITransactionAppService
{
	public async Task<TransactionListResponse> GetListAsync(Guid academicYearId, GetTransactionsRequest input, TransactionType transactionType)
	{
		await CheckPermissionAsync(transactionType, "View");
		if (input.Page < 1 || input.PageSize is < 1 or > 100)
			throw new BusinessException(TransactionErrorCodes.InvalidPaging);
		var scopedAccountIds = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync();
		var accounts = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetQueryableAsync();
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null) &&
			!await AsyncExecuter.AnyAsync(accounts.Where(a => a.AcademicYearId == academicYearId && scopedAccountIds.Contains(a.Id))))
		{
			return new TransactionListResponse
			{
				TotalCount = 0, CurrentPage = input.Page,
				PageSize = input.PageSize, TotalPages = 0, Items = []
			};
		}

		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
        var transactionQuery = await transactionRepository.GetQueryableAsync();
		var categoryQuery    = await transactionCategoryRepository.GetQueryableAsync();

		var joinedQuery =
			from t in transactionQuery
			join c in categoryQuery on t.CategoryId equals c.Id
			where t.AcademicYearId == academicYearId && t.TransactionType == transactionType && t.IsConfirmed &&
				(t.FinanceFeeAccountId.HasValue ? scopedAccountIds.Contains(t.FinanceFeeAccountId.Value) : t.StudentId == null || scopedStudentIds.Contains(t.StudentId.Value))
			select new { Transaction = t, Category = c };

		if (!input.Search.IsNullOrWhiteSpace())
		{
			var searchLower = input.Search.ToLower();
			joinedQuery = joinedQuery.Where(x => x.Transaction.Name.ToLower().Contains(searchLower));
		}

		var totalCount = await AsyncExecuter.CountAsync(joinedQuery);
		var rows = await AsyncExecuter.ToListAsync(
			joinedQuery
				.OrderByDescending(x => x.Transaction.CreationTime)
				.Skip((input.Page - 1) * input.PageSize)
				.Take(input.PageSize));

		return new TransactionListResponse
		{
			TotalCount  = totalCount, CurrentPage = input.Page,
			PageSize    = input.PageSize,
			TotalPages  = (int)Math.Ceiling(totalCount / (double)input.PageSize),
			Items       = rows.Select(x => MapToListItem(x.Transaction, x.Category)).ToList(),
		};
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<TransactionCreateUpdateResponse> CreateAsync(Guid academicYearId, TransactionCreateRequest input, TransactionType transactionType)
	{
        var paymentDate = input.PaymentDate.UtcDateTime;
		await CheckPermissionAsync(transactionType, "Add");
		Transaction.EnsureValidMoneyAndMethod(input.Amount, input.PaymentMethod);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			academicYearId, null, null, null, null);

		if (input.IdempotencyKey == Guid.Empty) throw new BusinessException(AccountsRegisterErrorCodes.ManualEntryKeyConflict);
        if (input.IdempotencyKey.HasValue)
        {
            var prior = await transactionRepository.FindAsync(input.IdempotencyKey.Value);
            if (prior != null)
            {
                prior.ExtraProperties.TryGetValue("PaymentReference", out var reference);
                prior.ExtraProperties.TryGetValue("Counterparty", out var counterparty);
                if (prior.IsAutomatic || prior.TenantId != CurrentTenant.Id || prior.AcademicYearId != academicYearId ||
                    prior.TransactionType != transactionType || prior.CategoryId != input.CategoryId || prior.Amount != input.Amount ||
                    prior.PaymentMethod != input.PaymentMethod || prior.PaymentDate != paymentDate || prior.BankAccountId != input.BankAccountId ||
                    prior.Name != input.Name || prior.Notes != input.Notes || reference?.ToString() != input.PaymentReference?.Trim() ||
                    counterparty?.ToString() != input.Counterparty?.Trim() || !prior.CategoryTags.Select(x => x.Value).OrderBy(x => x).SequenceEqual(input.CategoryTags.Select(x => new Tag(x).Value).Distinct().OrderBy(x => x)))
                    throw new BusinessException(AccountsRegisterErrorCodes.ManualEntryKeyConflict);
                return MapToCreateUpdateResponse(prior, await transactionCategoryRepository.GetAsync(prior.CategoryId));
            }
        }
		await referenceLock.AcquireAsync();
        var category = await transactionCategoryRepository.FindAsync(input.CategoryId)
			?? throw new BusinessException("ShikshaMitra:TransactionCategoryNotFound").WithData("CategoryId", input.CategoryId);

		if (category.CategoryType != transactionType)
			throw new BusinessException("ShikshaMitra:TransactionCategoryTypeMismatch")
				.WithData("CategoryId", category.Id).WithData("Expected", transactionType).WithData("Actual", category.CategoryType);
		await EnsureCategoryAvailableAsync(category, academicYearId, paymentDate, transactionType);

		var transaction = new Transaction(
			input.IdempotencyKey ?? GuidGenerator.Create(), CurrentTenant.Id, academicYearId, input.CategoryId,
			input.Name, input.CategoryTags, input.Amount,
			input.PaymentMethod, paymentDate, input.Notes, transactionType);

		if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 200 || input.Counterparty?.Length > 200 || input.PaymentReference?.Length > 100 || input.Notes?.Length > 2000)
            throw new BusinessException(AccountsRegisterErrorCodes.InvalidManualFields);
        var year = await academicYears.GetAsync(academicYearId);
        var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, null, null);
        var at = paymentDate;
        if (at < period.StartUtc || at >= period.EndUtc) throw new BusinessException(AccountsRegisterErrorCodes.InvalidPeriod);
        if (input.PaymentMethod == 0 && input.BankAccountId.HasValue) throw new BusinessException(AccountsRegisterErrorCodes.InvalidCashBank);
        var bank = input.PaymentMethod == 0 ? null : await routingManager.RequireBankAsync(academicYearId, input.BankAccountId, new DateTimeOffset(at));
        var label = bank == null ? null : $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}";
        transaction.SetManualRouting(category.Name, bank?.Id, label, input.PaymentReference);
        transaction.ExtraProperties["Counterparty"] = input.Counterparty?.Trim();
        await transactionRepository.InsertAsync(transaction, autoSave: true);
		return MapToCreateUpdateResponse(transaction, category);
	}

	public async Task<TransactionDetailsResponse> GetByIdAsync(Guid transactionId, TransactionType transactionType)
	{
		await CheckPermissionAsync(transactionType, "View");
		var transaction = await transactionRepository.GetAsync(transactionId);
        if (transaction.FinanceFeeAccountId.HasValue || transaction.StudentId.HasValue)
			await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(transaction.StudentId, transaction.FinanceFeeAccountId);
		await EnsureTransactionYearAsync(transaction);
		EnsureTransactionType(transaction, transactionType);

		var category = await transactionCategoryRepository.GetAsync(transaction.CategoryId);
		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var scopedAccountIds = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync();
        var transactionQuery  = await transactionRepository.GetQueryableAsync();
		var relatedTransactions = await AsyncExecuter.ToListAsync(
			transactionQuery.Where(x =>
				x.IsConfirmed && (x.FinanceFeeAccountId.HasValue ? scopedAccountIds.Contains(x.FinanceFeeAccountId.Value) : x.StudentId == null || scopedStudentIds.Contains(x.StudentId.Value)) && x.CategoryId == transaction.CategoryId &&
				x.AcademicYearId == transaction.AcademicYearId &&
				x.TransactionType == transactionType));

		return new TransactionDetailsResponse
		{
			TransactionInformation = new TransactionInformation
			{
				TransactionCategoryId   = category.Id,
				TransactionCategoryName = category.Name,
				CategoryTags            = ToTagValues(category.Tags),
				TotalAmountReceived     = relatedTransactions.Sum(x => x.Amount),
			},
			TransactionList = relatedTransactions.Select(MapToTransactionItem).ToList(),
		};
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<TransactionCreateUpdateResponse> UpdateAsync(Guid transactionId, TransactionUpdateRequest input, TransactionType transactionType)
	{
		await CheckPermissionAsync(transactionType, "Add");
		Transaction.EnsureValidMoneyAndMethod(input.Amount, input.PaymentMethod);
		var transaction = await transactionRepository.GetAsync(transactionId);
        if (transaction.FinanceFeeAccountId.HasValue || transaction.StudentId.HasValue)
			await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(transaction.StudentId, transaction.FinanceFeeAccountId);
		await EnsureTransactionYearAsync(transaction);
		EnsureTransactionType(transaction, transactionType);

		await referenceLock.AcquireAsync();
		var category = await transactionCategoryRepository.FindAsync(input.CategoryId)
			?? throw new BusinessException("ShikshaMitra:TransactionCategoryNotFound").WithData("CategoryId", input.CategoryId);

		if (category.CategoryType.HasValue && category.CategoryType.Value != transactionType)
			throw new BusinessException("ShikshaMitra:TransactionCategoryTypeMismatch")
				.WithData("CategoryId", category.Id).WithData("Expected", transactionType).WithData("Actual", category.CategoryType);
		await EnsureCategoryAvailableAsync(category, transaction.AcademicYearId, input.PaymentDate, transactionType);

		transaction.Update(input.CategoryId, input.Name, input.CategoryTags,
			input.Amount, input.PaymentMethod, input.PaymentDate, input.Notes);
		await transactionRepository.UpdateAsync(transaction, autoSave: true);
		return MapToCreateUpdateResponse(transaction, category);
	}

	public async Task<TransactionDeleteResponse> DeleteAsync(Guid transactionId, TransactionType transactionType)
	{
		await CheckPermissionAsync(transactionType, "Delete");
		var transaction = await transactionRepository.GetAsync(transactionId);
        if (transaction.FinanceFeeAccountId.HasValue || transaction.StudentId.HasValue)
			await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(transaction.StudentId, transaction.FinanceFeeAccountId);
		await EnsureTransactionYearAsync(transaction);
		EnsureTransactionType(transaction, transactionType);
		if (transaction.IsAutomatic) throw new BusinessException("Finance:AutomaticEntryImmutable");
        await transactionRepository.DeleteAsync(transaction, autoSave: true);
		return new TransactionDeleteResponse { Success = true, Message = "Transaction deleted successfully" };
	}

	private async Task EnsureCategoryAvailableAsync(TransactionCategory category, Guid academicYearId,
		DateTime paymentDate, TransactionType transactionType)
	{
		var postingTime = new DateTimeOffset(DateTime.SpecifyKind(paymentDate, DateTimeKind.Utc));
		if (!category.IsActive || category.AcademicYearId != academicYearId ||
			(category.EffectiveFrom.HasValue && postingTime < category.EffectiveFrom.Value) ||
			(category.EffectiveTo.HasValue && postingTime > category.EffectiveTo.Value))
		{
			throw new BusinessException("ShikshaMitra:TransactionCategoryNotAvailable")
				.WithData("CategoryId", category.Id);
		}
	}

	private Task EnsureTransactionYearAsync(Transaction transaction) => transaction.FinanceFeeAccountId.HasValue
		? financeScopeAccessService.EnsureCollectionYearAccessibleAsync(transaction.AcademicYearId)
		: financeScopeAccessService.EnsureAcademicContextAccessibleAsync(transaction.AcademicYearId, null, null, null, null);

	private Task CheckPermissionAsync(TransactionType type, string action)
	{
		var permission = type switch
		{
			TransactionType.Income => action switch
			{
				"View" => ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.View,
				"Add" => ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.Add,
				_ => ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.Delete
			},
			TransactionType.Expense => action switch
			{
				"View" => ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.View,
				"Add" => ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Add,
				_ => ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Delete
			},
			_ => throw new ArgumentOutOfRangeException(nameof(type))
		};
		return AuthorizationService.CheckAsync(permission);
	}

}
