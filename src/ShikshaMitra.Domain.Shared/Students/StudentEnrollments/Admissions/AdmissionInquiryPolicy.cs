using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionInquiryDuplicateRule
{
	Warn = 0,
	RequireReason = 1,
	Block = 2
}

public enum AdmissionInquiryApplicationStartRequirement
{
	Captured = 0,
	Contacted = 1,
	Qualified = 2
}

public enum AdmissionInquiryQualificationOutcome
{
	Qualified = 0,
	FollowUp = 1,
	NotInterested = 2,
	Lost = 3,
	Duplicate = 4
}

public enum AdmissionInquiryFollowUpStatus
{
	Scheduled = 0,
	Completed = 1,
	Cancelled = 2
}

public sealed record AdmissionInquiryQualificationCriterion
{
	public string Code { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public bool Required { get; init; }
}

public sealed record AdmissionInquiryPolicy
{
	public int Version { get; init; }
	public bool Enabled { get; init; }
	public bool RequireLinkedInquiry { get; init; } = true;
	public AdmissionInquiryApplicationStartRequirement ApplicationStartRequirement { get; init; } = AdmissionInquiryApplicationStartRequirement.Captured;
	public AdmissionInquiryDuplicateRule DuplicateRule { get; init; } = AdmissionInquiryDuplicateRule.RequireReason;
	public AdmissionCounsellorAssignmentMode AssignmentMode { get; init; } = AdmissionCounsellorAssignmentMode.Manual;
	public Guid? DefaultCounsellorId { get; init; }
	public List<Guid> CounsellorPoolUserIds { get; init; } = [];
	public bool RestrictActionsToAssignedCounsellor { get; init; }
	public int? FirstContactDueInHours { get; init; }
	public int? QualificationDueInDays { get; init; }
	public int FollowUpDueInDays { get; init; } = 3;
	public int MaxOpenFollowUps { get; init; } = 3;
	public bool RequireGuardianEmail { get; init; }
	public bool RequireApplicantDateOfBirth { get; init; }
	public List<AdmissionInquirySource> AllowedSources { get; init; } = [];
	public List<AdmissionInquirySource> RequireSourceDetailFor { get; init; } = [];
	public List<AdmissionInquiryQualificationCriterion> QualificationChecklist { get; init; } = [];

	// Legacy keys remain readable; intake behavior is now the same for every school.
	public AdmissionInquiryPolicy WithStandardBehavior() => this with
	{
		DuplicateRule = AdmissionInquiryDuplicateRule.RequireReason,
		ApplicationStartRequirement = AdmissionInquiryApplicationStartRequirement.Captured,
		RequireGuardianEmail = false,
		RequireApplicantDateOfBirth = false,
		RequireSourceDetailFor = []
	};

	public static AdmissionInquiryPolicy? Read(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("inquiry", out var value)) return null;
		var policy = value.Deserialize<AdmissionInquiryPolicy>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new ArgumentException("Inquiry settings are required.");
		if (!policy.Enabled) return null;
		policy = policy.WithStandardBehavior();
		policy.Validate();
		return policy;
	}

	public void Validate()
	{
		if (Version != 1) throw new ArgumentException("Inquiry version must be 1.");
		if (!Enum.IsDefined(ApplicationStartRequirement) || !Enum.IsDefined(DuplicateRule) || !Enum.IsDefined(AssignmentMode))
			throw new ArgumentException("Choose supported inquiry behavior.");
		if (DefaultCounsellorId == Guid.Empty || CounsellorPoolUserIds.Any(id => id == Guid.Empty))
			throw new ArgumentException("Choose valid counsellor identities.");
		if (CounsellorPoolUserIds.Distinct().Count() != CounsellorPoolUserIds.Count || CounsellorPoolUserIds.Count > 50)
			throw new ArgumentException("Choose up to 50 unique counsellors.");
		if (AssignmentMode == AdmissionCounsellorAssignmentMode.RoundRobin && CounsellorPoolUserIds.Count == 0)
			throw new ArgumentException("Choose at least one counsellor for round-robin assignment.");
		if (FirstContactDueInHours is < 1 or > 720)
			throw new ArgumentException("First contact time must be between 1 and 720 hours.");
		if (QualificationDueInDays is < 1 or > 365)
			throw new ArgumentException("Qualification time must be between 1 and 365 days.");
		if (FollowUpDueInDays is < 1 or > 90 || MaxOpenFollowUps is < 1 or > 20)
			throw new ArgumentException("Follow-up timing must use 1 to 90 days and allow 1 to 20 open follow-ups.");
		if (AllowedSources.Any(source => !Enum.IsDefined(source)) || RequireSourceDetailFor.Any(source => !Enum.IsDefined(source)) ||
			AllowedSources.Distinct().Count() != AllowedSources.Count || RequireSourceDetailFor.Distinct().Count() != RequireSourceDetailFor.Count)
			throw new ArgumentException("Choose unique supported inquiry sources.");
		if (AllowedSources.Count > 0 && RequireSourceDetailFor.Any(source => !AllowedSources.Contains(source)))
			throw new ArgumentException("Source details can only be required for an allowed source.");
		if (QualificationChecklist.Count > 30 || QualificationChecklist.Any(item =>
			item == null || !Regex.IsMatch(item.Code ?? string.Empty, "^[A-Z][A-Z0-9_]{0,63}$") ||
			string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 128) ||
			QualificationChecklist.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != QualificationChecklist.Count)
			throw new ArgumentException("Use up to 30 uniquely coded qualification checklist items.");
	}
}

public static class AdmissionInquiryManagementErrorCodes
{
	public const string InvalidPolicy = "Admissions:Inquiry:InvalidPolicy";
	public const string InvalidState = "Admissions:Inquiry:InvalidState";
	public const string ConcurrencyConflict = "Admissions:Inquiry:ConcurrencyConflict";
	public const string LinkedInquiryRequired = "Admissions:Inquiry:LinkedInquiryRequired";
	public const string ContactRequired = "Admissions:Inquiry:ContactRequired";
	public const string QualificationRequired = "Admissions:Inquiry:QualificationRequired";
	public const string ChecklistRequired = "Admissions:Inquiry:ChecklistRequired";
	public const string DuplicateBlocked = "Admissions:Inquiry:DuplicateBlocked";
	public const string DuplicateReasonRequired = "Admissions:Inquiry:DuplicateReasonRequired";
	public const string SourceNotAllowed = "Admissions:Inquiry:SourceNotAllowed";
	public const string SourceDetailRequired = "Admissions:Inquiry:SourceDetailRequired";
	public const string CounsellorRequired = "Admissions:Inquiry:CounsellorRequired";
	public const string FollowUpLimitReached = "Admissions:Inquiry:FollowUpLimitReached";
	public const string InvalidInput = "Admissions:Inquiry:InvalidInput";
}
