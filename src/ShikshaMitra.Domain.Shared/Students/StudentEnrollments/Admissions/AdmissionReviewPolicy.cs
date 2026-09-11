using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionReviewPolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public string? ReviewerRole { get; init; }
	public Guid? AssignedReviewerId { get; init; }
	public bool RequireIndependentApprover { get; init; }
	public string? ApproverRole { get; init; }
	public int? DueInDays { get; init; }
	public List<AdmissionReviewPrerequisite> Prerequisites { get; init; } = [];
	public List<AdmissionReviewChecklistItem> Checklist { get; init; } = [];
	public static AdmissionReviewPolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("review", out var value)) return null;
		var policy = value.Deserialize<AdmissionReviewPolicy>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new ArgumentException("Review settings are required.");
		if (!policy.Enabled) return null;
		policy.Validate(); return policy;
	}
	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Review version must be 1.");
		if (ReviewerRole?.Length > 256 || ApproverRole?.Length > 256 || AssignedReviewerId == Guid.Empty)
			throw new ArgumentException("Choose valid reviewer and approver identities.");
		if (DueInDays is < 1 or > 365) throw new ArgumentException("Review due time must be between 1 and 365 days.");
		if (Prerequisites == null || Prerequisites.Count > 20 || Checklist == null || Checklist.Count > 30)
			throw new ArgumentException("Use up to 20 prerequisites and 30 review checklist items.");
		if (Prerequisites.Any(item => item == null || item.Kind is not ("assessment" or "documents" or "eligibility") ||
			string.IsNullOrWhiteSpace(item.SourceStageCode) || item.SourceStageCode.Length > 64) ||
			Prerequisites.Select(item => item.SourceStageCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Prerequisites.Count)
			throw new ArgumentException("Select unique earlier managed prerequisite stages.");
		if (Checklist.Any(item => item == null || item.Code == null || !Regex.IsMatch(item.Code, "^[A-Z][A-Z0-9_]{0,63}$") ||
			string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 128) || Checklist.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Checklist.Count)
			throw new ArgumentException("Use unique uppercase checklist codes and names up to 128 characters.");
	}
}
public sealed record AdmissionReviewPrerequisite
{
	public string Kind { get; init; } = string.Empty;
	public string SourceStageCode { get; init; } = string.Empty;
}
public sealed record AdmissionReviewChecklistItem
{
	public string Code { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public bool Required { get; init; }
}
public static class AdmissionReviewErrorCodes
{
	public const string InvalidPolicy = "Admissions:Review:InvalidPolicy";
	public const string InvalidState = "Admissions:Review:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Review:ConcurrencyConflict";
	public const string ReviewRequired = "Admissions:Review:ReviewRequired";
	public const string StaleReview = "Admissions:Review:StaleReview";
	public const string EvidenceRequired = "Admissions:Review:EvidenceRequired";
	public const string ReviewerNotAllowed = "Admissions:Review:ReviewerNotAllowed";
	public const string DecisionNotAllowed = "Admissions:Review:DecisionNotAllowed";
	public const string InvalidInput = "Admissions:Review:InvalidInput";
	public const string RouteRequired = "Admissions:Review:RouteRequired";
}
