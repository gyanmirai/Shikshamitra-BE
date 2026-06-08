using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Transactions;

[RemoteService(false)]
public class TransactionAppService(
	IRepository<Transaction, Guid> transactionRepository,
	IRepository<TransactionCategory, Guid> transactionCategoryRepository,
	FinanceScopeAccessService financeScopeAccessService
) : ApplicationService, ITransactionAppService
{
	public async Task<TransactionListResponse> GetListAsync(Guid academicYearId, GetTransactionsRequest input, TransactionType transactionType)
	{
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			academicYearId,
			null,
			null,
			null,
			null);

		var transactionQuery = await transactionRepository.GetQueryableAsync();
		var categoryQuery = await transactionCategoryRepository.GetQueryableAsync();

		var joinedQuery =
			from t in transactionQuery
			join c in categoryQuery on t.CategoryId equals c.Id
			where t.AcademicYearId == academicYearId && t.TransactionType == transactionType
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
				.Take(input.PageSize)
		);

		return new TransactionListResponse
		{
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize),
			Items = rows.Select(x => MapToListItem(x.Transaction, x.Category)).ToList()
		};
	}

	public async Task<TransactionCreateUpdateResponse> CreateAsync(Guid academicYearId, TransactionCreateRequest input, TransactionType transactionType)
	{
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			academicYearId,
			null,
			null,
			null,
			null);

		var category = await transactionCategoryRepository.FindAsync(input.CategoryId)
			?? throw new BusinessException("ShikshaMitra:TransactionCategoryNotFound")
				.WithData("CategoryId", input.CategoryId);

		if (category.CategoryType.HasValue && category.CategoryType.Value != transactionType)
			throw new BusinessException("ShikshaMitra:TransactionCategoryTypeMismatch")
				.WithData("CategoryId", category.Id)
				.WithData("Expected", transactionType)
				.WithData("Actual", category.CategoryType);

		var transaction = new Transaction(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.CategoryId,
			input.Name,
			input.CategoryTags,
			(decimal) input.Amount,
			input.PaymentMethod,
			input.PaymentDate,
			input.Notes,
			transactionType
		);

		await transactionRepository.InsertAsync(transaction, autoSave: true);

		return MapToCreateUpdateResponse(transaction, category);
	}

	public async Task<TransactionDetailsResponse> GetByIdAsync(Guid transactionId, TransactionType transactionType)
	{
		var transaction = await transactionRepository.GetAsync(transactionId);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			transaction.AcademicYearId,
			null,
			null,
			null,
			null);

		EnsureTransactionType(transaction, transactionType);

		var category = await transactionCategoryRepository.GetAsync(transaction.CategoryId);

		var transactionQuery = await transactionRepository.GetQueryableAsync();
		var relatedTransactions = await AsyncExecuter.ToListAsync(
			transactionQuery.Where(x =>
				x.CategoryId == transaction.CategoryId &&
				x.AcademicYearId == transaction.AcademicYearId &&
				x.TransactionType == transactionType)
		);

		var totalAmount = relatedTransactions.Sum(x => x.Amount);

		return new TransactionDetailsResponse
		{
			TransactionInformation = new TransactionInformation
			{
				TransactionCategoryId = category.Id,
				TransactionCategoryName = category.Name,
				CategoryTags = ToTagValues(category.Tags),
				TotalAmountReceived = (double) totalAmount
			},
			TransactionList = relatedTransactions.Select(MapToTransactionItem).ToList()
		};
	}

	public async Task<TransactionCreateUpdateResponse> UpdateAsync(Guid transactionId, TransactionUpdateRequest input, TransactionType transactionType)
	{
		var transaction = await transactionRepository.GetAsync(transactionId);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			transaction.AcademicYearId,
			null,
			null,
			null,
			null);

		EnsureTransactionType(transaction, transactionType);

		var category = await transactionCategoryRepository.FindAsync(input.CategoryId)
			?? throw new BusinessException("ShikshaMitra:TransactionCategoryNotFound")
				.WithData("CategoryId", input.CategoryId);

		if (category.CategoryType.HasValue && category.CategoryType.Value != transactionType)
			throw new BusinessException("ShikshaMitra:TransactionCategoryTypeMismatch")
				.WithData("CategoryId", category.Id)
				.WithData("Expected", transactionType)
				.WithData("Actual", category.CategoryType);

		transaction.Update(
			input.CategoryId,
			input.Name,
			input.CategoryTags,
			(decimal) input.Amount,
			input.PaymentMethod,
			input.PaymentDate,
			input.Notes
		);

		await transactionRepository.UpdateAsync(transaction, autoSave: true);

		return MapToCreateUpdateResponse(transaction, category);
	}

	public async Task<TransactionDeleteResponse> DeleteAsync(Guid transactionId, TransactionType transactionType)
	{
		var transaction = await transactionRepository.GetAsync(transactionId);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			transaction.AcademicYearId,
			null,
			null,
			null,
			null);

		EnsureTransactionType(transaction, transactionType);

		await transactionRepository.DeleteAsync(transaction, autoSave: true);

		return new TransactionDeleteResponse
		{
			Success = true,
			Message = "Transaction deleted successfully"
		};
	}

	// ────────────────────────────────────────────────
	// Guards
	// ────────────────────────────────────────────────

	private static void EnsureTransactionType(Transaction transaction, TransactionType expected)
	{
		if (transaction.TransactionType != expected)
			throw new BusinessException("ShikshaMitra:TransactionTypeMismatch")
				.WithData("TransactionId", transaction.Id)
				.WithData("Expected", expected)
				.WithData("Actual", transaction.TransactionType);
	}

	// ────────────────────────────────────────────────
	// Mapping
	// ────────────────────────────────────────────────

	private static TransactionListItem MapToListItem(Transaction transaction, TransactionCategory category) =>
		new()
		{
			Id = transaction.Id,
			CategoryId = transaction.CategoryId,
			CategoryName = category.Name,
			CategoryTags = ToTagValues(transaction.CategoryTags),
			Name = transaction.Name,
			Amount = (double) transaction.Amount,
			PaymentMethod = transaction.PaymentMethod,
			PaymentDate = transaction.PaymentDate,
			Notes = transaction.Notes,
			CreatedAt = transaction.CreationTime
		};

	private static TransactionItem MapToTransactionItem(Transaction transaction) =>
		new()
		{
			TransactionId = transaction.Id,
			IncomeName = transaction.Name,
			ReceivedDate = transaction.PaymentDate,
			PaymentMethod = transaction.PaymentMethod,
			Amount = (double) transaction.Amount
		};

	private static TransactionCreateUpdateResponse MapToCreateUpdateResponse(Transaction transaction, TransactionCategory category) =>
		new()
		{
			Success = true,
			Data = new TransactionResponseData
			{
				Id = transaction.Id,
				CategoryId = transaction.CategoryId,
				CategoryName = category.Name,
				CategoryTags = ToTagValues(transaction.CategoryTags),
				Name = transaction.Name,
				Amount = (double) transaction.Amount,
				PaymentMethod = transaction.PaymentMethod,
				PaymentDate = transaction.PaymentDate,
				Notes = transaction.Notes,
				CreatedAt = transaction.CreationTime
			}
		};

	private static List<string> ToTagValues(IEnumerable<Tag> tags) =>
		tags.Select(t => t.Value).ToList();
}
