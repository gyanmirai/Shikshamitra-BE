using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
namespace ShikshaMitra.Finance.Accounts.BankActivity;
public class BankActivityQuery
{
	public Guid AcademicYearId { get; set; }
	public DateTimeOffset? From { get; set; }
	public DateTimeOffset? ToExclusive { get; set; }
	public string? Direction { get; set; }
	public string? Source { get; set; }
	public int? PaymentMethod { get; set; }
	public Guid? CategoryId { get; set; }
	public int? Status { get; set; }
	public string? Search { get; set; }
	public string? Filters { get; set; }
	public string? SortBy { get; set; }
	public string? SortOrder { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 25;
	public string? Panel { get; set; }
}
public class BankActivityPage
{
	public BankAccountResponse Account { get; set; } = new();
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = "";
	public string Currency { get; set; } = "INR";
	public DateTimeOffset From { get; set; }
	public DateTimeOffset ToExclusive { get; set; }
	public DateTimeOffset GeneratedAt { get; set; }
	public BankActivityCoverage Coverage { get; set; } = new();
	public BankActivityCapabilities Capabilities { get; set; } = new();
	public BankActivitySummary Summary { get; set; } = new();
	public List<BankActivityMethod> MethodBreakdown { get; set; } = [];
	public List<BankActivityCategory> Categories { get; set; } = [];
	public List<BankActivityRow> Items { get; set; } = [];
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
}
public class BankActivityCoverage
{
	public bool Income { get; set; }
	public bool Expense { get; set; }
	public bool Cash { get; set; }
	public bool ScopeLimited { get; set; } = true;
	public string Message { get; set; } = "Visible recorded activity only. Unassigned historical evidence is excluded; this is not a bank balance.";
}
public class BankActivityCapabilities
{
	public bool CanAddIncome { get; set; }
	public bool CanAddExpense { get; set; }
	public bool CanDepositCash { get; set; }
}
public class BankActivitySummary
{
	public decimal Incoming { get; set; }
	public decimal Outgoing { get; set; }
	public decimal Net { get; set; }
	public decimal PendingIncoming { get; set; }
	public decimal InTransit { get; set; }
}
public class BankActivityMethod : BankActivitySummary { public int PaymentMethod { get; set; } }
public record BankActivityCategory(Guid Id, string Label);
public class BankActivityRow
{
	public string Key { get; set; } = "";
	public string Source { get; set; } = "";
	public Guid SourceId { get; set; }
	public Guid? CashSourceId { get; set; }
	public string Reference { get; set; } = "";
	public string Counterparty { get; set; } = "";
	public string CategoryName { get; set; } = "";
	public int PaymentMethod { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
	public DateTimeOffset? OriginalPaymentAt { get; set; }
	public DateTimeOffset? BankCreditAt { get; set; }
	public decimal Incoming { get; set; }
	public decimal Outgoing { get; set; }
	public decimal PendingAmount { get; set; }
	public int RecognitionStatus { get; set; }
	public int BankConfirmationStatus { get; set; }
	public bool CanOpenSource { get; set; }
	public string? PaymentReference { get; set; }
	public string? CorrectsKey { get; set; }
	public Guid? DepositId { get; set; }
	public decimal? DepositAmount { get; set; }
	public decimal? CreditedAmount { get; set; }
	public decimal? InTransitAmount { get; set; }
	public DateTimeOffset? DepositDate { get; set; }
	public string? RecordedBy { get; set; }
}
public class BankActivityDetail : BankActivityRow
{
	public string? Notes { get; set; }
	public List<BankActivityAllocation> Allocations { get; set; } = [];
	public List<BankActivityCredit> Credits { get; set; } = [];
	public bool CanDownloadEvidence { get; set; }
	public string? SupportingDocumentName { get; set; }
}
public record BankActivityAllocation(string Category, decimal Amount);
public record BankActivityCredit(decimal Amount, DateTimeOffset Date, string Reference);
public class BankActivityExportRequest
{
	public BankActivityQuery Query { get; set; } = new();
	public string Format { get; set; } = "excel";
	public List<BankActivityExportColumn> Columns { get; set; } = [];
}
public record BankActivityExportColumn(string Id, string Label);
public interface IBankAccountActivityAppService : IApplicationService
{
	Task<BankActivityPage> GetAsync(Guid id, BankActivityQuery query);
	Task<BankActivityPage> GetDepositsAsync(Guid id, BankActivityQuery query);
	Task<BankActivityDetail> GetDetailAsync(Guid id, string movementKey, Guid academicYearId);
	Task<IRemoteStreamContent> ExportAsync(Guid id, BankActivityExportRequest input);
}
