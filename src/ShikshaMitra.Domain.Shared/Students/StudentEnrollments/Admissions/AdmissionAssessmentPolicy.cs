using System;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionAssessmentPolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public bool SchedulingEnabled { get; init; }
	public bool AttendanceRequired { get; init; }
	public bool ScoringEnabled { get; init; }
	public decimal TotalMarks { get; init; }
	public decimal PassingMarks { get; init; }
	public bool ReviewRequired { get; init; }
	public int MaxAttempts { get; init; }

	public static AdmissionAssessmentPolicy? Read(string? configurationJson)
	{
		if (string.IsNullOrWhiteSpace(configurationJson)) return null;
		using var document = JsonDocument.Parse(configurationJson);
		if (!document.RootElement.TryGetProperty("assessment", out var assessment)) return null;
		if (assessment.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("Assessment configuration must be an object.");
		var policy = assessment.Deserialize<AdmissionAssessmentPolicy>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new ArgumentException("Assessment configuration is required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Assessment configuration version must be 1.");
		if (MaxAttempts is < 1 or > 10) throw new ArgumentException("Assessment attempts must be between 1 and 10.");
		if (ScoringEnabled && (TotalMarks <= 0 || TotalMarks > 10000 || PassingMarks < 0 || PassingMarks > TotalMarks))
			throw new ArgumentException("Passing marks must be between zero and the positive total marks.");
	}
}

public static class AdmissionAssessmentErrorCodes
{
	public const string InvalidPolicy = "Admissions:Assessment:InvalidPolicy";
	public const string InvalidState = "Admissions:Assessment:InvalidState";
	public const string EvidenceRequired = "Admissions:Assessment:EvidenceRequired";
	public const string CapacityExceeded = "Admissions:Assessment:CapacityExceeded";
	public const string ConcurrencyConflict = "Admissions:Assessment:ConcurrencyConflict";
	public const string RouteRequired = "Admissions:Assessment:RouteRequired";
}
