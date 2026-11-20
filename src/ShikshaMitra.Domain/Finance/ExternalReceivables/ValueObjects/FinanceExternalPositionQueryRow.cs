using System;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

// Flat unmapped scalars retain provider-side count, sum, joins and paging.
public sealed class FinanceExternalPositionQueryRow
{
	public Guid SourceId { get; init; }
	public Guid? SourceLinkId { get; init; }
	public Guid? AcademicYearId { get; init; }
	public FinanceExternalPositionEvidence Evidence { get; init; }
	public string? ReviewCode { get; init; }
	public FinanceExternalRecordedSettlementDeclaration RecordedSettlementDeclaration { get; init; }
	public decimal AssessmentOriginal { get; init; }
	public string? Currency { get; init; }
	public ChargeStatus? ApprovalStatus { get; init; }
	public decimal? Original { get; init; }
	public decimal? Accepted { get; init; }
	public decimal? Settled { get; init; }
	public decimal? Pending { get; init; }
	public decimal? NetWaived { get; init; }
	public decimal? FinancialOutstanding { get; init; }
	public decimal? Collectible { get; init; }
	public decimal? Refunded { get; init; }
}
