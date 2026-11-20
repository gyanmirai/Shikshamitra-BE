using System;

namespace ShikshaMitra.Finance.ExternalReceivables;

// The same unmapped complete raw facts feed query and bounded detail reads.
// Finance obtains accepted reason origin from its immutable snapshot itself.
public sealed class FinanceExternalPositionSource
{
	public Guid TenantId { get; init; }
	public FinanceExternalSourceKind Kind { get; init; }
	public Guid SourceId { get; init; }
	public string? SourceVersion { get; init; }
	public Guid? ExpectedSourceLinkId { get; init; }
	public Guid? AcademicYearId { get; init; }
	public bool AcademicYearConfirmed { get; init; }
	public string? AcademicYearEvidenceJson { get; init; }
	public FinanceExternalPayerKind PayerKind { get; init; }
	public Guid? StudentId { get; init; }
	public Guid? EmployeeId { get; init; }
	public decimal AssessmentOriginal { get; init; }
	public string? RecordedCurrency { get; init; }
	public DateOnly? SchoolAssessmentDate { get; init; }
	public DateOnly? SchoolDueDate { get; init; }
	public bool SchoolDatesConfirmed { get; init; }
	public string? SourceDateEvidenceJson { get; init; }
	public Guid RawSourceCategoryId { get; init; }
	public string? RawSourceRemarks { get; init; }
	public string? CurrentLegacyResolvedReason { get; init; }
	public FinanceExternalRecordedSettlementDeclaration RecordedSettlementDeclaration { get; init; }
}
