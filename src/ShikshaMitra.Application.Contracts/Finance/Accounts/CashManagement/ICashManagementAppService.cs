using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public interface ICashManagementAppService : IApplicationService
{
	Task<CashPage> GetListAsync(CashQuery input);
	Task<CashDetail> GetAsync(Guid id, Guid academicYearId);
	Task<CashOptions> GetOptionsAsync(Guid academicYearId, string operation);
	Task<CashDetail> ApproveAsync(Guid id, CashApproval input);
	Task<CashDetail> DepositAsync(Guid id, CashDepositInput input);
	Task<CashDetail> ConfirmAsync(Guid id, Guid depositId, CashCreditInput input);
}

public class CashQuery
{
	public Guid AcademicYearId { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
	public int? Status { get; set; }
	public string? Search { get; set; }
	public string? Filters { get; set; }
	public string? SortBy { get; set; }
	public string? SortOrder { get; set; }
}
public class CashPage
{
	public List<CashRow> Items { get; set; } = [];
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public CashSummary Summary { get; set; } = new();
	public CashStatusCounts StatusCounts { get; set; } = new();
}
public class CashStatusCounts
{
	public int All { get; set; }
	public int Pending { get; set; }
	public int Approved { get; set; }
}
public class CashSummary
{
	public decimal Collected { get; set; }
	public decimal PendingApproval { get; set; }
	public decimal Approved { get; set; }
	public decimal Spent { get; set; }
	public decimal Deposited { get; set; }
	public decimal InTransit { get; set; }
	public decimal Remaining { get; set; }
}
public class CashRow
{
	public Guid Id { get; set; }
	public Guid SourceId { get; set; }
	public string Reference { get; set; } = "";
	public string PayerName { get; set; } = "";
	public decimal Amount { get; set; }
	public decimal SpentAmount { get; set; }
	public decimal DepositedAmount { get; set; }
	public decimal InTransitAmount { get; set; }
	public decimal RemainingAmount { get; set; }
	public int Status { get; set; }
	public Guid? BankAccountId { get; set; }
	public string? BankLabel { get; set; }
	public DateTimeOffset CollectedAt { get; set; }
	public DateTimeOffset? ApprovedAt { get; set; }
	public Guid? CreatorId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public bool CanApprove { get; set; }
	public bool CanDeposit { get; set; }
}
public class CashDetail : CashRow
{
	public string? Notes { get; set; }
	public Guid? ApprovedBy { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? FinanceFeeAccountId { get; set; }
	public List<CashDeposit> Deposits { get; set; } = [];
	public List<CashExpense> Expenses { get; set; } = [];
}
public record CashDeposit(Guid Id, decimal Amount, decimal CreditedAmount, int Status, string Reference,
	DateTimeOffset DepositDate, DateTimeOffset? CreditDate, Guid CreatedBy, bool CanConfirm);
public record CashExpense(Guid Id, string Name, decimal Amount, DateTimeOffset PaymentDate);
public record CashBankOption(Guid Id, string Label);
public record CashSourceOption(Guid Id, string Label, decimal RemainingAmount, string ConcurrencyStamp);
public record CashOptions(List<CashBankOption> BankAccounts, List<CashSourceOption> CashSources);
public class CashCommandInput
{
	public Guid AcademicYearId { get; set; }
	public Guid IdempotencyKey { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
}
public class CashApproval : CashCommandInput
{
	public Guid BankAccountId { get; set; }
	public decimal CountedAmount { get; set; }
	public string? Notes { get; set; }
}
public class CashDepositInput : CashCommandInput
{
	public decimal Amount { get; set; }
	public DateTimeOffset DepositDate { get; set; }
	public string Reference { get; set; } = "";
	public string? Notes { get; set; }
}
public class CashCreditInput : CashCommandInput
{
	public decimal Amount { get; set; }
	public DateTimeOffset CreditDate { get; set; }
	public string Reference { get; set; } = "";
	public string? Notes { get; set; }
}
