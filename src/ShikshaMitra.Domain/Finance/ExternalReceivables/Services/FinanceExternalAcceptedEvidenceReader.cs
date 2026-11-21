using System;
using System.Globalization;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceExternalAcceptedEvidenceReader
{
	public static string? NormalizeReason(string? value) => value?.Trim(FinanceExternalEvidenceJson.WhiteSpaceCharacters.ToCharArray());
	public static FinanceExternalAcceptedEvidence Read(FinanceExternalReceivableSourceLink link, Charge? charge)
	{
		var json = link.SourceSnapshotJson;
		string? Scalar(string path, string kind = "string") => FinanceExternalEvidenceJson.Scalar(json, path, kind);
		string? Key(string path) => Scalar(path)?.ToLowerInvariant();
		bool Null(string path) => Scalar(path, "null") == "null";
		var originRemarks = Scalar("ReasonOrigin.RawRemarks");
		var originRemarksNull = Null("ReasonOrigin.RawRemarks");
		return new()
		{
			LinkId = link.Id, TenantId = link.TenantId, SourceKind = link.SourceKind, SourceId = link.SourceId,
			SourceVersion = link.SourceVersion, AcademicYearId = link.AcademicYearId, Currency = link.Currency,
			StoredFingerprint = link.SourceFingerprint, CalculatedFingerprint = FinanceExternalSourceFingerprintV1.TryFromSnapshot(json),
			FinanceChargeId = link.FinanceChargeId, RequestId = link.FinanceChargeRequestId,
			ActualChargeId = charge?.Id, ActualRequestId = charge?.ChargeRequestId, ActualYearId = charge?.AcademicYearId,
			StudentId = charge?.StudentId, EmployeeId = charge?.EmployeeId, Amount = charge?.Amount, Status = charge?.Status,
			SchoolAssessmentDate = charge?.SchoolAssessmentDate, SchoolDueDate = charge?.SchoolDueDate,
			TenantKey = link.TenantId?.ToString("D"), SourceKey = link.SourceId.ToString("D"), YearKey = link.AcademicYearId.ToString("D"),
			StudentKey = charge?.StudentId?.ToString("D"), EmployeeKey = charge?.EmployeeId?.ToString("D"),
			AssessmentDateText = charge?.SchoolAssessmentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			DueDateText = charge?.SchoolDueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			SnapshotTenant = Key("TenantId"), SnapshotKind = Scalar("Source.Kind", "number"),
			SnapshotSource = Key("Source.SourceId"), SnapshotVersion = Scalar("Source.SourceVersion"),
			SnapshotYear = Key("AcademicYearId"), SnapshotYearConfirmed = Scalar("AcademicYearConfirmed", "boolean"),
			SnapshotYearEvidence = Scalar("AcademicYearEvidenceJson"),
			SnapshotYearHasEvidence = !string.IsNullOrWhiteSpace(Scalar("AcademicYearEvidenceJson")),
			SnapshotPayerKind = Scalar("Payer.Kind", "number"), SnapshotStudent = Key("Payer.StudentId"),
			SnapshotStudentNull = Null("Payer.StudentId"), SnapshotEmployee = Key("Payer.EmployeeId"),
			SnapshotEmployeeNull = Null("Payer.EmployeeId"), SnapshotAmount = FinanceExternalEvidenceJson.Money(json, "Amount"),
			SnapshotCurrency = Scalar("Currency"), SnapshotAssessmentDate = Scalar("AssessmentDate"), SnapshotDueDate = Scalar("DueDate"),
			SnapshotDatesConfirmed = Scalar("SchoolDatesConfirmed", "boolean"), SnapshotReason = NormalizeReason(Scalar("Reason")),
			SnapshotDateEvidence = Scalar("EvidenceReference"), SnapshotDateHasEvidence = !string.IsNullOrWhiteSpace(Scalar("EvidenceReference")),
			OriginKind = OriginKind(json), OriginSchema = Scalar("ReasonOrigin.Schema", "number"),
			OriginCategory = Key("ReasonOrigin.CategoryId"), OriginRemarks = originRemarks, OriginRemarksNull = originRemarksNull,
			OriginRemarksHasText = originRemarks != null ? !string.IsNullOrWhiteSpace(originRemarks) : originRemarksNull ? false : null,
			OriginUsesCategory = Scalar("ReasonOrigin.UsesCategoryReason", "boolean")
		};
	}

	private static string? OriginKind(string json)
	{
		if (FinanceExternalEvidenceJson.Scalar(json, "Source.SourceId", "string") == null) return "invalid";
		try
		{
			using var document = JsonDocument.Parse(json);
			if (!document.RootElement.TryGetProperty("ReasonOrigin", out var origin)) return null;
			return origin.ValueKind == JsonValueKind.Object ? "object" : "invalid";
		}
		catch (JsonException) { return "invalid"; }
	}
}
