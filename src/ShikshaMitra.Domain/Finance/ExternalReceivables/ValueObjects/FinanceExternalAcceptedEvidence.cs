using System;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

// Unmapped accepted evidence. Null markers keep missing/incorrect JSON kinds
// distinct from legitimate null payer identifiers.
public sealed class FinanceExternalAcceptedEvidence
{
	public Guid? LinkId { get; init; }
	public Guid? TenantId { get; init; }
	public FinanceExternalSourceKind? SourceKind { get; init; }
	public Guid? SourceId { get; init; }
	public string? SourceVersion { get; init; }
	public string? StoredFingerprint { get; init; }
	public string? CalculatedFingerprint { get; init; }
	public Guid? AcademicYearId { get; init; }
	public string? Currency { get; init; }
	public Guid? FinanceChargeId { get; init; }
	public Guid? RequestId { get; init; }
	public Guid? ActualChargeId { get; init; }
	public Guid? ActualRequestId { get; init; }
	public Guid? ActualYearId { get; init; }
	public Guid? StudentId { get; init; }
	public Guid? EmployeeId { get; init; }
	public decimal? Amount { get; init; }
	public ChargeStatus? Status { get; init; }
	public DateOnly? SchoolAssessmentDate { get; init; }
	public DateOnly? SchoolDueDate { get; init; }
	public string? TenantKey { get; init; }
	public string? SourceKey { get; init; }
	public string? YearKey { get; init; }
	public string? StudentKey { get; init; }
	public string? EmployeeKey { get; init; }
	public string? AssessmentDateText { get; init; }
	public string? DueDateText { get; init; }
	public string? SnapshotTenant { get; init; }
	public string? SnapshotKind { get; init; }
	public string? SnapshotSource { get; init; }
	public string? SnapshotVersion { get; init; }
	public string? SnapshotYear { get; init; }
	public string? SnapshotYearConfirmed { get; init; }
	public string? SnapshotYearEvidence { get; init; }
	public bool SnapshotYearHasEvidence { get; init; }
	public string? SnapshotPayerKind { get; init; }
	public string? SnapshotStudent { get; init; }
	public bool SnapshotStudentNull { get; init; }
	public string? SnapshotEmployee { get; init; }
	public bool SnapshotEmployeeNull { get; init; }
	public decimal? SnapshotAmount { get; init; }
	public string? SnapshotCurrency { get; init; }
	public string? SnapshotAssessmentDate { get; init; }
	public string? SnapshotDueDate { get; init; }
	public string? SnapshotDatesConfirmed { get; init; }
	public string? SnapshotReason { get; init; }
	public string? SnapshotDateEvidence { get; init; }
	public bool SnapshotDateHasEvidence { get; init; }
	public string? OriginKind { get; init; }
	public string? OriginSchema { get; init; }
	public string? OriginCategory { get; init; }
	public string? OriginRemarks { get; init; }
	public bool OriginRemarksNull { get; init; }
	public bool? OriginRemarksHasText { get; init; }
	public string? OriginUsesCategory { get; init; }
}
