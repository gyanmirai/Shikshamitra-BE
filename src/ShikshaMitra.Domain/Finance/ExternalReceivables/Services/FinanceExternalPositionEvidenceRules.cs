using System;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceExternalPositionEvidenceRules
{
	public static readonly Expression<Func<FinanceExternalPositionSource, FinanceExternalAcceptedEvidence, string?, bool>> Matches =
		(source, accepted, reason) =>
		accepted.LinkId.HasValue && source.ExpectedSourceLinkId == accepted.LinkId &&
		accepted.StoredFingerprint != null && accepted.CalculatedFingerprint != null && accepted.StoredFingerprint == accepted.CalculatedFingerprint &&
		source.TenantId == accepted.TenantId && source.Kind == FinanceExternalSourceKind.LibraryAssessment &&
		accepted.SourceKind == source.Kind && accepted.SourceId == source.SourceId && source.SourceId != Guid.Empty &&
		source.SourceVersion != null && source.SourceVersion != "" && source.SourceVersion.Length <= 64 &&
		source.SourceVersion == accepted.SourceVersion && accepted.SnapshotVersion == accepted.SourceVersion &&
		accepted.SnapshotTenant == accepted.TenantKey && accepted.SnapshotSource == accepted.SourceKey &&
		accepted.SnapshotKind == "1" && source.AcademicYearConfirmed && source.AcademicYearId.HasValue &&
		source.AcademicYearId != Guid.Empty && source.AcademicYearId == accepted.AcademicYearId &&
		accepted.ActualYearId == accepted.AcademicYearId && accepted.SnapshotYear == accepted.YearKey &&
		accepted.SnapshotYearConfirmed == "true" && accepted.SnapshotYearHasEvidence &&
		accepted.SnapshotYearEvidence == source.AcademicYearEvidenceJson &&
		accepted.ActualChargeId.HasValue && accepted.ActualChargeId == accepted.FinanceChargeId &&
		accepted.ActualRequestId == accepted.RequestId && accepted.Amount == source.AssessmentOriginal &&
		accepted.SnapshotAmount == accepted.Amount &&
		(accepted.Status == ChargeStatus.Pending || accepted.Status == ChargeStatus.Approved ||
		 accepted.Status == ChargeStatus.Rejected || accepted.Status == ChargeStatus.Voided) &&
		source.RecordedCurrency == "INR" && accepted.Currency == "INR" && accepted.SnapshotCurrency == "INR" &&
		source.SchoolDatesConfirmed && source.SchoolAssessmentDate.HasValue && source.SchoolDueDate.HasValue &&
		source.SchoolAssessmentDate != DateOnly.MinValue && source.SchoolDueDate != DateOnly.MinValue &&
		source.SchoolDueDate >= source.SchoolAssessmentDate && source.SchoolAssessmentDate == accepted.SchoolAssessmentDate &&
		source.SchoolDueDate == accepted.SchoolDueDate && accepted.SnapshotAssessmentDate == accepted.AssessmentDateText &&
		accepted.SnapshotDueDate == accepted.DueDateText && accepted.SnapshotDatesConfirmed == "true" &&
		accepted.SnapshotDateHasEvidence && accepted.SnapshotDateEvidence == source.SourceDateEvidenceJson &&
		accepted.SnapshotReason != null && accepted.SnapshotReason != "" &&
		(source.PayerKind == FinanceExternalPayerKind.Student && source.StudentId.HasValue && source.StudentId != Guid.Empty &&
			!source.EmployeeId.HasValue && source.StudentId == accepted.StudentId && !accepted.EmployeeId.HasValue &&
			accepted.SnapshotPayerKind == "1" && accepted.SnapshotStudent == accepted.StudentKey && accepted.SnapshotEmployeeNull ||
		 source.PayerKind == FinanceExternalPayerKind.Employee && source.EmployeeId.HasValue && source.EmployeeId != Guid.Empty &&
			!source.StudentId.HasValue && source.EmployeeId == accepted.EmployeeId && !accepted.StudentId.HasValue &&
			accepted.SnapshotPayerKind == "2" && accepted.SnapshotEmployee == accepted.EmployeeKey && accepted.SnapshotStudentNull) &&
		(accepted.OriginKind == null && reason == accepted.SnapshotReason ||
		 accepted.OriginKind == "object" && accepted.OriginSchema == "1" &&
			source.RawSourceCategoryId.ToString().ToLower().CompareTo(accepted.OriginCategory) == 0 &&
			(source.RawSourceRemarks == null && accepted.OriginRemarksNull ||
			 source.RawSourceRemarks != null && accepted.OriginRemarks != null && source.RawSourceRemarks == accepted.OriginRemarks) &&
			(accepted.OriginUsesCategory == "true" && accepted.OriginRemarksHasText == false ||
			 accepted.OriginUsesCategory == "false" && accepted.OriginRemarksHasText == true && reason == accepted.SnapshotReason));

	private static readonly Func<FinanceExternalPositionSource, FinanceExternalAcceptedEvidence, string?, bool> MatchValue = Matches.Compile();
	public static bool IsMatch(FinanceExternalPositionSource source, FinanceExternalAcceptedEvidence accepted, string? reason) =>
		MatchValue(source, accepted, reason);
}
