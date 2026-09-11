using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionFormStagePolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public string Responsibility { get; init; } = "applicant";
	public bool AllowSaveAndResume { get; init; } = true;
	public int? DueInDays { get; init; }
	public bool AllowLateSubmission { get; init; } = true;
	public bool AutoAdvanceOnSubmit { get; init; } = true;
	public bool AllowCorrectionRequests { get; init; } = true;
	public int MaxSubmissionAttempts { get; init; } = 3;
	public bool RequireSectionVerification { get; init; }
	public List<string> VerificationSectionCodes { get; init; } = [];

	public static AdmissionFormStagePolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("form", out var value)) return null;
		if (value.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Form stage settings must be an object.");
		var policy = value.Deserialize<AdmissionFormStagePolicy>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new ArgumentException("Form stage settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Form stage settings version must be 1.");
		if (Responsibility is not ("applicant" or "guardian" or "staff" or "shared"))
			throw new ArgumentException("Choose applicant, guardian, staff, or shared responsibility.");
		if (DueInDays is < 1 or > 365)
			throw new ArgumentException("Form due time must be between 1 and 365 days.");
		if (MaxSubmissionAttempts is < 1 or > 20)
			throw new ArgumentException("Form submission attempts must be between 1 and 20.");
		if (AllowCorrectionRequests && MaxSubmissionAttempts < 2)
			throw new ArgumentException("Allow at least two submission attempts when corrections are enabled.");
		if (VerificationSectionCodes == null || VerificationSectionCodes.Count > 30 ||
			VerificationSectionCodes.Any(code => code == null || !Regex.IsMatch(code, "^[A-Z][A-Z0-9_-]{0,63}$")) ||
			VerificationSectionCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != VerificationSectionCodes.Count)
			throw new ArgumentException("Choose up to 30 unique form sections for verification.");
		if (RequireSectionVerification && VerificationSectionCodes.Count == 0)
			throw new ArgumentException("Choose at least one section when verification is required.");
		if (!RequireSectionVerification && VerificationSectionCodes.Count > 0)
			throw new ArgumentException("Section choices belong to required verification only.");
	}
}

public static class AdmissionFormStageErrorCodes
{
	public const string InvalidPolicy = "Admissions:FormStage:InvalidPolicy";
	public const string InvalidState = "Admissions:FormStage:InvalidState";
	public const string ConcurrencyConflict = "Admissions:FormStage:ConcurrencyConflict";
	public const string DeadlinePassed = "Admissions:FormStage:DeadlinePassed";
	public const string AttemptLimitReached = "Admissions:FormStage:AttemptLimitReached";
	public const string VerificationRequired = "Admissions:FormStage:VerificationRequired";
	public const string InvalidSection = "Admissions:FormStage:InvalidSection";
	public const string CorrectionNotAllowed = "Admissions:FormStage:CorrectionNotAllowed";
	public const string OfferingFormMismatch = "Admissions:FormStage:OfferingFormMismatch";
}
