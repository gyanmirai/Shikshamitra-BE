using System;
using System.Linq;
using System.Threading.Tasks;
namespace ShikshaMitra.Finance.Accounts.BankActivity;
/// <summary>Provider-composable read facts. No stored ledger or tracked entities.</summary>
public sealed class BankActivityFact
{
	public string Key { get; set; } = "";
	public string Source { get; set; } = "";
	public Guid SourceId { get; set; }
	public Guid? StudentId { get; set; }
	public Guid? FinanceFeeAccountId { get; set; }
	public Guid? CashSourceId { get; set; }
	public string Reference { get; set; } = "";
	public string Counterparty { get; set; } = "";
	public string CategoryName { get; set; } = "";
	public string CategoryIds { get; set; } = "";
	public string AllocationsJson { get; set; } = "[]";
	public int PaymentMethod { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
	public DateTimeOffset? OriginalPaymentAt { get; set; }
	public DateTimeOffset? BankCreditAt { get; set; }
	public decimal Incoming { get; set; }
	public decimal Outgoing { get; set; }
	public decimal PendingAmount { get; set; }
	public int RecognitionStatus { get; set; }
	public int BankConfirmationStatus { get; set; }
	public string? PaymentReference { get; set; }
	public string? CorrectsKey { get; set; }
	public Guid? DepositId { get; set; }
	public decimal DepositAmount { get; set; }
	public decimal CreditedAmount { get; set; }
	public decimal InTransitAmount { get; set; }
	public DateTimeOffset? DepositDate { get; set; }
	public string? RecordedBy { get; set; }
	public string? Notes { get; set; }
	public string Panel { get; set; } = "activity";
}
public interface IBankActivityQuerySource
{
	Task<IQueryable<BankActivityFact>> QueryAsync(Guid? tenantId, Guid academicYearId, Guid bankId);
}
