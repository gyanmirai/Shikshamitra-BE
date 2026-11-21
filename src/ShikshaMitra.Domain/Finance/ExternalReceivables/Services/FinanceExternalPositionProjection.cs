using System;
using System.Linq.Expressions;
using ShikshaMitra.Library;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceExternalPositionProjection
{
	public static readonly Expression<Func<FinanceExternalPositionAggregate, bool>> SourceValid = value =>
		FinanceExternalPositionEvidenceRules.IsMatch(value.Source, value.Accepted, value.CurrentReason);
	public static readonly Expression<Func<FinanceExternalPositionAggregate, bool>> Valid = value =>
		IsSourceValid(value) && !value.InvalidAllocation && !value.InvalidWaiver && !value.InvalidRefund &&
		FinanceExternalBalanceExpressions.HasStoredRange(value.Balance) &&
		FinanceExternalBalanceExpressions.HasValidStructure(value.Balance) && value.Refunded >= 0m && value.Refunded <= value.Balance.Settled;
	public static readonly Expression<Func<FinanceExternalPositionAggregate, bool>> Unposted = value =>
		!value.Source.ExpectedSourceLinkId.HasValue && !value.Accepted.LinkId.HasValue;
	public static readonly Expression<Func<FinanceExternalPositionAggregate, FinanceExternalPositionQueryRow>> Row = value => new()
	{
		SourceId = value.Source.SourceId, SourceLinkId = value.Source.ExpectedSourceLinkId ?? value.Accepted.LinkId,
		AcademicYearId = value.Source.AcademicYearId, RecordedSettlementDeclaration = value.Source.RecordedSettlementDeclaration,
		AssessmentOriginal = value.Source.AssessmentOriginal,
		Evidence = IsUnposted(value) ? FinanceExternalPositionEvidence.Unposted :
			IsValid(value) ? FinanceExternalPositionEvidence.Verified : FinanceExternalPositionEvidence.NeedsReview,
		ReviewCode = IsUnposted(value) || IsValid(value) ? null :
			IsSourceValid(value) ? LibraryFinanceErrorCodes.InvalidBalance : LibraryFinanceErrorCodes.SourceRequiresReview,
		Currency = IsValid(value) ? value.Accepted.Currency : value.Source.RecordedCurrency,
		ApprovalStatus = IsValid(value) ? value.Accepted.Status : null,
		Original = IsValid(value) ? value.Balance.Original : null,
		Accepted = IsValid(value) ? value.Balance.Accepted : null,
		Settled = IsValid(value) ? value.Balance.Settled : null,
		Pending = IsValid(value) ? value.Balance.Pending : null,
		NetWaived = IsValid(value) ? FinanceExternalBalanceExpressions.Waived(value.Balance) : null,
		FinancialOutstanding = IsValid(value) ? FinanceExternalBalanceExpressions.Unsettled(value.Balance) : null,
		Collectible = IsValid(value) ? FinanceExternalBalanceExpressions.Available(value.Balance) : null,
		Refunded = IsValid(value) ? value.Refunded : null
	};
	private static readonly Func<FinanceExternalPositionAggregate, bool> SourceValue = SourceValid.Compile();
	private static readonly Func<FinanceExternalPositionAggregate, bool> ValidValue = Valid.Compile();
	private static readonly Func<FinanceExternalPositionAggregate, bool> UnpostedValue = Unposted.Compile();
	private static readonly Func<FinanceExternalPositionAggregate, FinanceExternalPositionQueryRow> RowValue = Row.Compile();
	public static bool IsSourceValid(FinanceExternalPositionAggregate value) => SourceValue(value);
	public static bool IsValid(FinanceExternalPositionAggregate value) => ValidValue(value);
	public static bool IsUnposted(FinanceExternalPositionAggregate value) => UnpostedValue(value);
	public static FinanceExternalPositionQueryRow Project(FinanceExternalPositionAggregate value) => RowValue(value);
}
