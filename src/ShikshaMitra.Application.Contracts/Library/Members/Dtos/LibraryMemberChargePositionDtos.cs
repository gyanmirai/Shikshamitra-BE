using System.Collections.Generic;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Library.Members.Dtos;

public sealed class LibraryMemberChargePositionDto
{
	public FinanceExternalPositionEvidence Evidence { get; set; }
	public ChargeStatus? ApprovalStatus { get; set; }
	public string? Currency { get; set; }
	public LibraryMemberChargeAmountsDto? Amounts { get; set; }
	public IReadOnlyList<string> ReviewIssues { get; set; } = [];
}

public sealed class LibraryMemberChargeAmountsDto
{
	public string OriginalAmountText { get; set; } = string.Empty;
	public string AcceptedAmountText { get; set; } = string.Empty;
	public string SettledAmountText { get; set; } = string.Empty;
	public string PendingVerificationAmountText { get; set; } = string.Empty;
	public string NetWaivedAmountText { get; set; } = string.Empty;
	public string FinancialOutstandingAmountText { get; set; } = string.Empty;
	public string CollectibleAmountText { get; set; } = string.Empty;
	public string RefundedAmountText { get; set; } = string.Empty;
}
