using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;
namespace ShikshaMitra.Finance.Accounts.BankActivity;
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
public partial class BankAccountActivityAppService(IBankActivityQuerySource source,
 IRepository<BankAccount, Guid> banks, IRepository<AcademicYear, Guid> years,
 IRepository<TransactionCategory, Guid> categories, FinanceScopeAccessService scope, IUserScopeService userScopes,
 IUnitOfWorkManager units) : ApplicationService, IBankAccountActivityAppService
{
	private sealed record Context(BankAccount Bank, AcademicYear Year, IQueryable<BankActivityFact> Facts, BankActivityCoverage Coverage, BankActivityCapabilities Capabilities);
	[UnitOfWork(IsDisabled = true)]
	public async Task<BankActivityPage> GetAsync(Guid id, BankActivityQuery query)
	{
		using var snapshot = units.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }, requiresNew: true);
		var context = await ContextAsync(id, query.AcademicYearId);
		var page = await PageAsync(context, query);
		await snapshot.CompleteAsync(); return page;
	}
	[UnitOfWork(IsDisabled = true)]
	public Task<BankActivityPage> GetDepositsAsync(Guid id, BankActivityQuery query)
	{ query.Panel = "deposits"; return GetAsync(id, query); }
	[UnitOfWork(IsDisabled = true)]
	public async Task<BankActivityDetail> GetDetailAsync(Guid id, string movementKey, Guid academicYearId)
	{
		if (string.IsNullOrEmpty(movementKey) || movementKey.Length > 200) throw new BusinessException(BankActivityErrorCodes.InvalidQuery);
		using var snapshot = units.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }, requiresNew: true);
		var context = await ContextAsync(id, academicYearId);
		var fact = await AsyncExecuter.FirstOrDefaultAsync(context.Facts.Where(x => x.Key == movementKey)) ?? throw new EntityNotFoundException();
		var detail = await MapAsync(fact);
		if (!detail.CanOpenSource) throw new AbpAuthorizationException();
		detail.Notes = fact.Notes;
		detail.Allocations = (JsonSerializer.Deserialize<List<Allocation>>(fact.AllocationsJson, JsonOptions) ?? [])
		 .GroupBy(x => x.Category).Select(g => new BankActivityAllocation(g.Key, g.Sum(x => x.Amount))).ToList();
		if (fact.DepositId.HasValue)
		{
			var credits = await AsyncExecuter.ToListAsync(context.Facts.Where(x => x.Panel == "activity" && x.DepositId == fact.DepositId && x.BankConfirmationStatus == (int) BankActivityConfirmationStatus.CreditConfirmed));
			detail.Credits = credits.OrderBy(x => x.OccurredAt).Select(x => new BankActivityCredit(x.Incoming, x.OccurredAt, Mask(x.Reference))).ToList();
		}
		// Source files can cover a whole pooled batch and must not be delegated to partial-scope viewers.
		// The owning Cash Management endpoint remains the authorized download boundary.
		detail.CanDownloadEvidence = false;
		await snapshot.CompleteAsync(); return detail;
	}
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
	private sealed record Allocation(Guid Id, string Category, decimal Amount);
	private async Task<Context> ContextAsync(Guid id, Guid academicYearId)
	{
		if (academicYearId == Guid.Empty || id == Guid.Empty) throw new BusinessException(BankActivityErrorCodes.InvalidQuery);
		await scope.EnsureCollectionYearAccessibleAsync(academicYearId);
		var bank = await banks.FindAsync(x => x.Id == id && x.AcademicYearId == academicYearId && x.TenantId == CurrentTenant.Id) ?? throw new AbpAuthorizationException();
		var year = await years.FindAsync(x => x.Id == academicYearId && x.TenantId == CurrentTenant.Id) ?? throw new AbpAuthorizationException();
		var income = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.View);
		var expense = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.View);
		var cash = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement.View);
		if (!income && !expense && !cash) throw new AbpAuthorizationException();
		var facts = await source.QueryAsync(CurrentTenant.Id, academicYearId, id);
		facts = BankActivityQueryRules.AuthorizedSlices(facts, income, expense, cash, await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View));
		var current = await userScopes.GetUserScopeAsync(CurrentUser.GetId());
		facts = BankActivityQueryRules.Scope(facts, await scope.GetScopedStudentIdsQueryAsync(), await scope.GetScopedFeeAccountIdsQueryAsync(), current.HasAcademicYearWideScope(academicYearId));
		var at = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var enabled = bank.IsActive && (!bank.EffectiveFrom.HasValue || bank.EffectiveFrom <= at) && (!bank.EffectiveTo.HasValue || bank.EffectiveTo >= at);
		return new(bank, year, facts, new() { Income = income, Expense = expense, Cash = cash }, new()
		{
			CanAddIncome = enabled && income && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.Add),
			CanAddExpense = enabled && expense && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.Add),
			CanDepositCash = enabled && cash && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement.Deposit)
		});
	}
	private async Task<BankActivityPage> PageAsync(Context context, BankActivityQuery input)
	{
		var table = BankActivityQueryRules.Validate(input);
		var offset = TimeSpan.FromMinutes(330);
		var now = new DateTimeOffset(Clock.Now.ToUniversalTime()).ToOffset(offset);
		input.From ??= new DateTimeOffset(DateTime.SpecifyKind(context.Year.StartDate.Date, DateTimeKind.Unspecified), offset);
		input.ToExclusive ??= new DateTimeOffset(now.Date.AddDays(1), offset);
		if (input.From >= input.ToExclusive || input.ToExclusive - input.From > TimeSpan.FromDays(36600)) throw new BusinessException(BankActivityErrorCodes.InvalidQuery);
		var query = BankActivityQueryRules.Filter(context.Facts, input, table);
		var summary = await AsyncExecuter.FirstOrDefaultAsync(BankActivityQueryRules.Aggregates(query)) ?? new BankActivitySummary();
		var methods = await AsyncExecuter.ToListAsync(query.GroupBy(x => x.PaymentMethod).Select(g => new BankActivityMethod
		{ PaymentMethod = g.Key, Incoming = g.Sum(x => x.Incoming), Outgoing = g.Sum(x => x.Outgoing), Net = g.Sum(x => x.Incoming - x.Outgoing), PendingIncoming = g.Sum(x => x.PendingAmount), InTransit = g.Sum(x => x.InTransitAmount) }));
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(BankActivityQueryRules.Order(query, table).Skip((input.Page - 1) * input.PageSize).Take(input.PageSize));
		var items = new List<BankActivityRow>(); foreach (var row in rows) items.Add(await MapAsync(row));
		var categoryQuery = (await categories.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.AcademicYearId == input.AcademicYearId && query.Any(f => f.CategoryIds.Contains(c.Id.ToString())));
		var options = await AsyncExecuter.ToListAsync(categoryQuery.OrderBy(c => c.Name).Select(c => new BankActivityCategory(c.Id, c.Name)).Take(1001));
		if (options.Count > 1000) throw new BusinessException(BankActivityErrorCodes.ExportLimit);
		return new()
		{
			Account = Masked(context.Bank),
			AcademicYearId = context.Year.Id,
			AcademicYearName = context.Year.Name,
			From = input.From.Value,
			ToExclusive = input.ToExclusive.Value,
			GeneratedAt = new DateTimeOffset(Clock.Now.ToUniversalTime()),
			Coverage = context.Coverage,
			Capabilities = context.Capabilities,
			Summary = summary,
			MethodBreakdown = methods,
			Categories = options,
			Items = items,
			TotalCount = total,
			Page = input.Page,
			PageSize = input.PageSize
		};
	}
	private async Task<BankActivityDetail> MapAsync(BankActivityFact fact) => new()
	{
		Key = fact.Key,
		Source = fact.Source,
		SourceId = fact.SourceId,
		CashSourceId = fact.CashSourceId,
		Reference = fact.Reference,
		Counterparty = fact.Counterparty,
		CategoryName = fact.CategoryName,
		PaymentMethod = fact.PaymentMethod,
		OccurredAt = fact.OccurredAt,
		OriginalPaymentAt = fact.OriginalPaymentAt,
		BankCreditAt = fact.BankCreditAt,
		Incoming = fact.Incoming,
		Outgoing = fact.Outgoing,
		PendingAmount = fact.PendingAmount,
		RecognitionStatus = fact.RecognitionStatus,
		BankConfirmationStatus = fact.BankConfirmationStatus,
		PaymentReference = Mask(fact.PaymentReference),
		CorrectsKey = fact.CorrectsKey,
		DepositId = fact.DepositId,
		DepositAmount = fact.DepositId.HasValue ? fact.DepositAmount : null,
		CreditedAmount = fact.DepositId.HasValue ? fact.CreditedAmount : null,
		InTransitAmount = fact.DepositId.HasValue ? fact.InTransitAmount : null,
		DepositDate = fact.DepositDate,
		RecordedBy = fact.RecordedBy,
		CanOpenSource = fact.Source switch
		{
			"collection" => await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View),
			"refund" => await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View),
			_ => true
		}
	};
	private static string Mask(string? value) => string.IsNullOrEmpty(value) ? "" : value.Length <= 4 ? new string('*', value.Length) : new string('*', value.Length - 4) + value[^4..];
	private static BankAccountResponse Masked(BankAccount bank) => new()
	{
		Id = bank.Id,
		AcademicYearId = bank.AcademicYearId,
		Code = bank.Code,
		BankName = bank.BankName,
		BranchName = bank.BranchName,
		BranchAddress = bank.BranchAddress,
		AccountHolderName = bank.AccountHolderName,
		AccountType = bank.AccountType,
		MaskedAccountNumber = Mask(bank.AccountNumber),
		Purpose = bank.Purpose,
		IfscCode = string.IsNullOrEmpty(bank.IfscCode) ? "" : bank.IfscCode.Length <= 4 ? new string('*', bank.IfscCode.Length) : bank.IfscCode[..4] + new string('*', bank.IfscCode.Length - 4),
		MaskedUpiId = string.IsNullOrWhiteSpace(bank.UpiId) ? null : bank.UpiId.Contains('@') ? "***" + bank.UpiId[bank.UpiId.IndexOf('@')..] : "***",
		EffectiveFrom = bank.EffectiveFrom,
		EffectiveTo = bank.EffectiveTo,
		IsActive = bank.IsActive,
		Version = bank.Version,
		ConcurrencyStamp = bank.ConcurrencyStamp
	};
}
