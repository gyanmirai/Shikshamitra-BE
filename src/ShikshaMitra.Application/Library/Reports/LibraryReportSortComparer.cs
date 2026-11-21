using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShikshaMitra.Library.Reports;

internal static class LibraryReportSortComparer
{
	private static readonly HashSet<string> NumericColumns = new(StringComparer.OrdinalIgnoreCase)
	{
		"price", "pages", "totalCopies", "availableCopies", "issuedCopies", "reservedCopies", "lostCopies", "damagedCopies",
		"underRepairCopies", "discardedCopies", "inCustodyCopies", "inTransitCopies", "bookCount", "copyNumber", "issueCount", "activeIssues", "overdueIssues",
		"reservationCount", "pendingCharges", "amount", "overdueDays", "issueDateProvenance", "dueDateProvenance",
		"returnDateProvenance", "requestedDateProvenance", "requestExpiryDateProvenance", "state",
		"originalAmountText", "acceptedAmountText", "settledAmountText", "pendingVerificationAmountText",
		"netWaivedAmountText", "financialOutstandingAmountText", "collectibleAmountText", "refundedAmountText"
	};
	private static readonly IComparer<string> Numeric = new NumericComparer();
	public static IComparer<string> For(string column) => NumericColumns.Contains(column) ? Numeric : StringComparer.CurrentCulture;

	private sealed class NumericComparer : IComparer<string>
	{
		public int Compare(string? left, string? right)
		{
			var hasLeft = decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var a);
			var hasRight = decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var b);
			if (hasLeft && hasRight) return a.CompareTo(b);
			if (hasLeft != hasRight) return hasLeft ? 1 : -1;
			return StringComparer.CurrentCulture.Compare(left, right);
		}
	}
}
