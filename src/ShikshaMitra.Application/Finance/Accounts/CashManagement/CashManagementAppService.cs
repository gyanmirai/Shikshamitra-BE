using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

[Authorize, RemoteService(false)]
public partial class CashManagementAppService(IRepository<CashCollection, Guid> sources,
	IRepository<Transaction, Guid> transactions, IRepository<BankAccount, Guid> banks,
	IRepository<AcademicYear, Guid> years, FinanceScopeAccessService scope, FinanceRoutingManager routing,
	ICashCollectionLock sourceLock, ICashCommandStore commands, IUnitOfWorkManager uows,
	IUserScopeService userScopes) : ApplicationService, ICashManagementAppService
{
	public async Task<CashPage> GetListAsync(CashQuery input)
	{
		await AuthorizationService.CheckAsync(P.View);
		if (input.Page < 1 || input.PageSize is < 1 or > 100 || input.Status is < 0 or > 1 || input.Search?.Length > 200)
			throw new BusinessException(CashManagementErrorCodes.InvalidQuery);
		var query = await ScopedQueryAsync(input.AcademicYearId);
		var yearSummary = await SummarizeAsync(query);
		query = await ApplyDisplayFiltersAsync(query, input);
		var table = FinanceTableQuery.Parse(input.Filters, input.Search, input.SortBy, input.SortOrder, TableColumns);
		query = table.Apply(query, TableFields);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Reference.ToLower().Contains(search) || x.PayerName.ToLower().Contains(search));
		}
		var statusCounts = await AsyncExecuter.ToListAsync(query.GroupBy(x => x.Status)
			.Select(group => new { Status = group.Key, Count = group.Count() }));
		var counts = new CashStatusCounts
		{
			All = statusCounts.Sum(x => x.Count),
			Pending = statusCounts.Where(x => x.Status == 0).Sum(x => x.Count),
			Approved = statusCounts.Where(x => x.Status == 1).Sum(x => x.Count)
		};
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status);
		var total = await AsyncExecuter.CountAsync(query);
		// All seven totals use the identical scoped/filter predicate as the register.
		var facts = await AsyncExecuter.ToListAsync(query.Select(x => new { x.Amount, x.Status, x.SpentAmount, x.DepositedAmount, x.InTransitAmount }));
		var totals = new CashSummary
		{
			Collected = facts.Sum(x => x.Amount), PendingApproval = facts.Where(x => x.Status == 0).Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount),
			Approved = facts.Where(x => x.Status == 1).Sum(x => x.Amount), Spent = facts.Sum(x => x.SpentAmount),
			Deposited = facts.Sum(x => x.DepositedAmount), InTransit = facts.Sum(x => x.InTransitAmount),
			Remaining = facts.Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount)
		};
		var page = await AsyncExecuter.ToListAsync(table.Order(query, TableFields, "creationTime", true, x => x.Id)
			.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize));
		var flags = await FlagsAsync();
		return new CashPage { Items = page.Select(x => (CashRow)Map(x, flags)).ToList(), TotalCount = total,
			Page = input.Page, PageSize = input.PageSize, Summary = totals, YearSummary = yearSummary,
			AvailableCash = yearSummary.Remaining, StatusCounts = counts };
	}
	public async Task<CashDetail> GetAsync(Guid id, Guid academicYearId)
	{
		await AuthorizationService.CheckAsync(P.View);
		return Map(await RequireAsync(id, academicYearId), await FlagsAsync());
	}
	public async Task<CashOptions> GetOptionsAsync(Guid academicYearId, string operation)
	{
		await AuthorizationService.CheckAsync(operation switch
		{
			"approve" => P.Approve, "deposit" => P.Deposit,
			"expense" => ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Add,
			_ => throw new AbpAuthorizationException()
		});
		var query = await ScopedQueryAsync(academicYearId);
		var available = operation == "approve" ? [] : await AsyncExecuter.ToListAsync(query.Where(x => (operation == "expense" || x.Status == 1) &&
			x.Amount > x.SpentAmount + x.DepositedAmount + x.InTransitAmount).OrderBy(x => x.CreationTime));
		var bankRows = operation == "expense" ? [] : await banks.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.IsActive);
		var now = Now;
		return new CashOptions(bankRows.Where(x => !(x.EffectiveFrom > now || x.EffectiveTo < now))
			.OrderBy(x => x.BankName).Select(x => new CashBankOption(x.Id, BankLabel(x))).ToList(),
			available.Select(x => new CashSourceOption(x.Id, $"{x.Reference} • {x.PayerName}", x.RemainingAmount, x.ConcurrencyStamp)).ToList());
	}
	public Task<CashDetail> ApproveAsync(Guid id, CashApproval input) => ExecuteAsync(id, input, P.Approve, "approve", async cash =>
	{
		var approvedAt = Now;
		await EnsureActualDateAsync(input.AcademicYearId, approvedAt);
		var bank = await routing.RequireBankAsync(input.AcademicYearId, input.BankAccountId, approvedAt);
		var priorDeposits = cash.Deposits.Count;
		cash.Approve(Actor, bank.Id, BankLabel(bank), input.CountedAmount, approvedAt, input.Notes,
			allowSelfApproval: await userScopes.IsAdminUserAsync(Actor));
		if (cash.Deposits.Count > priorDeposits)
			cash.ExtraProperties["DepositBank:" + cash.Deposits.Last().Id.ToString("N")] = JsonSerializer.Serialize(new CashBankOption(bank.Id, BankLabel(bank)));
		await FinalizeIncomeAsync(cash, bank);
	});
	public Task<CashDetail> DepositAsync(Guid id, CashDepositInput input) => ExecuteAsync(id, input, P.Deposit, "deposit", async cash =>
	{
		await EnsureActualDateAsync(input.AcademicYearId, input.DepositDate);
		var deposit = cash.Dispatch(GuidGenerator.Create(), Actor, input.Amount, input.DepositDate, input.Reference, input.Notes);
		if (cash.BankAccountId.HasValue)
			cash.ExtraProperties["DepositBank:" + deposit.Id.ToString("N")] = JsonSerializer.Serialize(new CashBankOption(cash.BankAccountId.Value, cash.BankLabel ?? ""));
	});
	public Task<CashDetail> ConfirmAsync(Guid id, Guid depositId, CashCreditInput input) => ExecuteAsync(id, input, P.Reconcile,
		"confirm:" + depositId.ToString("N"), async cash =>
	{
		await EnsureActualDateAsync(input.AcademicYearId, input.CreditDate);
		cash.Credit(depositId, Actor, input.Amount, input.CreditDate, input.Reference, input.Notes);
	});
	private async Task<CashDetail> ExecuteAsync<T>(Guid id, T input, string permission, string action, Func<CashCollection, Task> transition)
		where T : CashCommandInput
	{
		await AuthorizationService.CheckAsync(permission);
		using var uow = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await sourceLock.AcquireAsync(input.AcademicYearId);
		await sourceLock.AcquireAsync(id);
		var cash = await RequireForUpdateAsync(id, input.AcademicYearId);
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(action + ":" + Actor + ":" + JsonSerializer.Serialize(input))));
		var prior = cash.Replay(input.IdempotencyKey, fingerprint);
		if (prior != null)
		{
			await uow.CompleteAsync();
			return JsonSerializer.Deserialize<CashDetail>(prior)!;
		}
		cash.EnsureStamp(input.ConcurrencyStamp);
		await transition(cash);
		// ABP assigns the persisted concurrency stamp during SaveChanges. Bind the response
		// after that save and append replay evidence without rotating the parent again.
		await sources.UpdateAsync(cash, autoSave: true);
		var result = Map(cash, await FlagsAsync());
		await commands.AppendAsync(cash.Id, input.IdempotencyKey, fingerprint, JsonSerializer.Serialize(result));
		await uow.CompleteAsync();
		return result;
	}
	private Guid Actor => CurrentUser.Id ?? throw new AbpAuthorizationException();
	private DateTimeOffset Now => new(Clock.Now.ToUniversalTime());
	private static string BankLabel(BankAccount bank) => $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}";
	private async Task EnsureActualDateAsync(Guid yearId, DateTimeOffset date)
	{
		var year = await years.GetAsync(yearId);
		var period = ShikshaMitra.Finance.Accounts.AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, null, null);
		if (date == default || date > Now || date.UtcDateTime < period.StartUtc || date.UtcDateTime >= period.EndUtc)
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
	}
	private async Task<IQueryable<CashCollection>> ScopedQueryAsync(Guid yearId)
	{
		await scope.EnsureCollectionYearAccessibleAsync(yearId);
		var students = await scope.GetScopedStudentIdsQueryAsync();
		var accounts = await scope.GetScopedFeeAccountIdsQueryAsync();
		var manualAccessible = await scope.IsAcademicContextAccessibleAsync(yearId, null, null, null, null);
		return (await sources.WithDetailsAsync(x => x.Deposits, x => x.Expenses, x => x.Commands))
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId &&
				(x.FinanceFeeAccountId.HasValue ? accounts.Contains(x.FinanceFeeAccountId.Value) :
					x.StudentId.HasValue ? students.Contains(x.StudentId.Value) : manualAccessible));
	}
	internal async Task<CashCollection> RequireAsync(Guid id, Guid yearId) =>
		await AsyncExecuter.FirstOrDefaultAsync((await ScopedQueryAsync(yearId)).Where(x => x.Id == id))
		?? throw new BusinessException(CashManagementErrorCodes.InvalidSource);
	private async Task<CashCollection> RequireForUpdateAsync(Guid id, Guid yearId)
	{
		if (!await AsyncExecuter.AnyAsync((await ScopedQueryAsync(yearId)).Where(x => x.Id == id)))
			throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		// The scoped student subquery uses AsNoTracking, which EF applies to the whole
		// composed query. After authorization, load a tracked graph in this same locked
		// serializable transaction so new owned deposits/credits are detected as Added.
		using var tracking = sources.EnableTracking();
		return await AsyncExecuter.FirstOrDefaultAsync((await sources.WithDetailsAsync(x => x.Deposits, x => x.Expenses, x => x.Commands))
			.Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId))
			?? throw new BusinessException(CashManagementErrorCodes.InvalidSource);
	}
	private async Task<(bool Approve, bool Deposit, bool Credit, bool SelfApproval)> FlagsAsync()
	{
		var approve = await AuthorizationService.IsGrantedAsync(P.Approve);
		return (approve, await AuthorizationService.IsGrantedAsync(P.Deposit), await AuthorizationService.IsGrantedAsync(P.Reconcile),
			approve && await userScopes.IsAdminUserAsync(Actor));
	}
	private CashDetail Map(CashCollection cash, (bool Approve, bool Deposit, bool Credit, bool SelfApproval) flags) => new()
	{
		SupportingDocumentName = cash.ExtraProperties.TryGetValue("SupportingDocumentName", out var documentName) ? documentName?.ToString() : null,
		Id = cash.Id, SourceId = cash.SourceId, Reference = cash.Reference, PayerName = cash.PayerName,
		Amount = cash.Amount, SpentAmount = cash.SpentAmount, DepositedAmount = cash.DepositedAmount,
		InTransitAmount = cash.InTransitAmount, RemainingAmount = cash.RemainingAmount, Status = cash.Status,
		BankAccountId = LatestBank(cash)?.Id ?? cash.BankAccountId, BankLabel = LatestBank(cash)?.Label ?? cash.BankLabel, CollectedAt = cash.CollectedAt,
		ApprovedAt = cash.ApprovedAt, CreatorId = cash.CollectedBy, ConcurrencyStamp = cash.ConcurrencyStamp,
		CanApprove = flags.Approve && cash.Status == 0 && (cash.CollectedBy != Actor || flags.SelfApproval),
		CanDeposit = flags.Deposit && cash.Status == 1 && cash.RemainingAmount > 0,
		Notes = cash.Notes, ApprovedBy = cash.ApprovedBy, StudentId = cash.StudentId, FinanceFeeAccountId = cash.FinanceFeeAccountId,
		Deposits = cash.Deposits.OrderBy(x => x.DepositDate).Select(x => new CashDeposit(x.Id, x.Amount, x.CreditedAmount,
			x.Amount == x.CreditedAmount ? 1 : 0, x.Reference, x.DepositDate, x.CreditDate, x.CreatedBy,
			flags.Credit && x.CreditedAmount < x.Amount && x.CreatedBy != Actor,
			DepositBank(cash, x.Id)?.Id ?? cash.BankAccountId, DepositBank(cash, x.Id)?.Label ?? cash.BankLabel)).ToList(),
		Expenses = cash.Expenses.OrderBy(x => x.PaymentDate).Select(x => new CashExpense(ExpenseTransactionId(cash, x.Id), x.Name, x.Amount, x.PaymentDate)).ToList()
	};
	private static Guid ExpenseTransactionId(CashCollection cash, Guid allocationId) =>
		cash.ExtraProperties.TryGetValue("ExpenseTransaction:" + allocationId.ToString("N"), out var value) &&
		Guid.TryParse(value?.ToString(), out var transactionId) ? transactionId : allocationId;
	private static CashBankOption? DepositBank(CashCollection cash, Guid depositId) =>
		cash.ExtraProperties.TryGetValue("DepositBank:" + depositId.ToString("N"), out var bank) && bank != null
			? JsonSerializer.Deserialize<CashBankOption>(bank.ToString()!) : null;
	private static CashBankOption? LatestBank(CashCollection cash) => cash.Deposits
		.OrderByDescending(x => x.DepositDate).ThenByDescending(x => x.Id)
		.Select(x => DepositBank(cash, x.Id)).FirstOrDefault(x => x != null);
}
