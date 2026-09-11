using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionEligibilityPolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public bool AllowExceptions { get; init; }
	public string? ExceptionApprovalRole { get; init; }
	public bool RequireIndependentReviewer { get; init; } = true;
	public List<AdmissionEligibilityCriterion> Criteria { get; init; } = [];
	public static AdmissionEligibilityPolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("eligibility", out var value)) return null;
		var policy = value.Deserialize<AdmissionEligibilityPolicy>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new ArgumentException("Eligibility settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate();
		return policy;
	}
	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Eligibility version must be 1.");
		if (ExceptionApprovalRole?.Length > 256) throw new ArgumentException("The exception approval role allows 256 characters.");
		if (Criteria == null || Criteria.Count is < 1 or > 30) throw new ArgumentException("Configure between 1 and 30 eligibility criteria.");
		var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var criterion in Criteria)
		{
			if (criterion == null) throw new ArgumentException("Eligibility criteria must be objects.");
			criterion.Validate();
			if (!codes.Add(criterion.Code)) throw new ArgumentException("Eligibility criterion codes must be unique.");
		}
	}
}

public sealed record AdmissionEligibilityCriterion
{
	public string Code { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string Kind { get; init; } = string.Empty;
	public string? FailureMessage { get; init; }
	public int? MinAgeMonths { get; init; }
	public int? MaxAgeMonths { get; init; }
	public string? CutoffDate { get; init; }
	public Guid? FormDefinitionId { get; init; }
	public string? ConditionJson { get; init; }
	public string? SourceStageCode { get; init; }
	public decimal? MinimumPercentage { get; init; }
	public string? RequiredOutcome { get; init; }
	public void Validate()
	{
		if (Code == null || !Regex.IsMatch(Code, "^[A-Z][A-Z0-9_]{0,63}$")) throw new ArgumentException("Use a unique uppercase eligibility code, up to 64 characters.");
		if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || FailureMessage?.Length > 1024) throw new ArgumentException("Provide a criterion name (128 characters maximum) and a message up to 1024 characters.");
		if (Kind is not ("age" or "application" or "inquiry" or "assessment" or "documents")) throw new ArgumentException("Choose a supported eligibility criterion.");
		if (Kind == "age")
		{
			if (!MinAgeMonths.HasValue && !MaxAgeMonths.HasValue || MinAgeMonths is < 0 or > 1200 || MaxAgeMonths is < 0 or > 1200 || MinAgeMonths > MaxAgeMonths)
				throw new ArgumentException("Choose a minimum or maximum age between 0 and 1200 months, with minimum no greater than maximum.");
			if (!DateOnly.TryParseExact(CutoffDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Choose a fixed eligibility cutoff date.");
		}
		if (Kind is "application" or "inquiry")
		{
			if (!FormDefinitionId.HasValue || FormDefinitionId == Guid.Empty) throw new ArgumentException("Select the published source form.");
			if (AdmissionDocumentCondition.GetFields(ConditionJson).Any(field => !field.StartsWith(Kind + ".", StringComparison.OrdinalIgnoreCase)))
				throw new ArgumentException("Each answer criterion can reference only its selected form source.");
		}
		if (Kind is "assessment" or "documents")
		{
			if (string.IsNullOrWhiteSpace(SourceStageCode) || SourceStageCode.Length > 64) throw new ArgumentException("Select an earlier managed source stage.");
			if (Kind == "assessment" && (RequiredOutcome != null && RequiredOutcome is not ("passed" or "waived") || MinimumPercentage is < 0 or > 100))
				throw new ArgumentException("Choose a passed or waived assessment outcome and a percentage between 0 and 100.");
			if (RequiredOutcome == "waived" && MinimumPercentage.HasValue) throw new ArgumentException("A waived assessment has no percentage result.");
			if (Kind == "documents" && RequiredOutcome != null && RequiredOutcome != "verified") throw new ArgumentException("Document eligibility requires verified evidence.");
		}
	}
}

public static class AdmissionEligibilityErrorCodes
{
	public const string InvalidPolicy = "Admissions:Eligibility:InvalidPolicy";
	public const string OfferingFormMismatch = "Admissions:Eligibility:OfferingFormMismatch";
	public const string InvalidState = "Admissions:Eligibility:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Eligibility:ConcurrencyConflict";
	public const string EvaluationRequired = "Admissions:Eligibility:EvaluationRequired";
	public const string StaleEvaluation = "Admissions:Eligibility:StaleEvaluation";
	public const string ExceptionNotAllowed = "Admissions:Eligibility:ExceptionNotAllowed";
	public const string RouteRequired = "Admissions:Eligibility:RouteRequired";
}
