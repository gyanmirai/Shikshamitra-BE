using System.Collections.Generic;

namespace ShikshaMitra.Library.Dashboard.Dtos;

public sealed class LibraryFinancePositionSummaryDto
{
	public int VerifiedCount { get; set; }
	public int NeedsReviewCount { get; set; }
	public int UnpostedCount { get; set; }
	public List<LibraryFinanceCurrencyTotalsDto> CurrencyTotals { get; set; } = [];
}

public sealed class LibraryFinanceCurrencyTotalsDto
{
	public string Currency { get; set; } = string.Empty;
	public string OriginalAmountText { get; set; } = string.Empty;
	public string AcceptedAmountText { get; set; } = string.Empty;
	public string SettledAmountText { get; set; } = string.Empty;
	public string PendingVerificationAmountText { get; set; } = string.Empty;
	public string NetWaivedAmountText { get; set; } = string.Empty;
	public string FinancialOutstandingAmountText { get; set; } = string.Empty;
	public string CollectibleAmountText { get; set; } = string.Empty;
	public string RefundedAmountText { get; set; } = string.Empty;
}
