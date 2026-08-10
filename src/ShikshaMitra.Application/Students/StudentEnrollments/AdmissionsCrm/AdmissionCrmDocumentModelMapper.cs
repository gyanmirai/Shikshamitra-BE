using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmDocumentModelMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmDocumentChecklistTemplateUpsertDto ToTemplateUpsert(AdmissionCrmDocumentChecklistTemplate value) => new()
	{
		Name = value.Name,
		IsFallback = value.IsFallback,
		Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(value.ApplicabilityJson),
		Requirements = Deserialize<List<AdmissionCrmDocumentRequirementDto>>(value.RequirementsJson),
		ReadinessApprovalCount = value.ReadinessApprovalCount,
		Notes = value.Notes
	};

	public static AdmissionCrmDocumentChecklistTemplateDto ToTemplateDto(AdmissionCrmDocumentChecklistTemplate value)
	{
		var input = ToTemplateUpsert(value);
		return new AdmissionCrmDocumentChecklistTemplateDto
		{
			Id = value.Id,
			AcademicYearId = value.AcademicYearId,
			Version = value.Version,
			Status = value.Status,
			PublishedAt = value.PublishedAt,
			RetiredAt = value.RetiredAt,
			Name = input.Name,
			IsFallback = input.IsFallback,
			Applicability = input.Applicability,
			Requirements = input.Requirements,
			ReadinessApprovalCount = input.ReadinessApprovalCount,
			Notes = input.Notes
		};
	}

	public static AdmissionCrmApplicantDocumentDto ToDocumentDto(
		AdmissionCrmDocumentRequirementDto requirement,
		AdmissionCrmApplicantDocument? document,
		bool required) => new()
	{
		Id = document?.Id,
		RequirementKey = requirement.Key,
		RequirementName = requirement.Name,
		RequirementLevel = requirement.RequirementLevel,
		IsCurrentlyRequired = required,
		BlocksAdmissionReadiness = requirement.BlocksAdmissionReadiness,
		RequiresOriginal = requirement.RequiresOriginal,
		ValidityDateRequired = requirement.ValidityDateRequired,
		ExpiryReminderDaysBefore = requirement.ExpiryReminderDaysBefore,
		OriginalSeen = document?.OriginalSeenAt.HasValue == true || document?.Status == AdmissionCrmApplicantDocumentStatus.OriginalSeen,
		Status = document?.ValidUntil < DateTime.UtcNow ? AdmissionCrmApplicantDocumentStatus.Expired : document?.Status,
		Version = document?.Version ?? 0,
		FileName = document?.FileName,
		ContentType = document?.ContentType,
		FileSize = document?.FileSize,
		ValidUntil = document?.ValidUntil,
		UploadedAt = document?.UploadedAt,
		DecisionReason = document?.DecisionReason,
		DecisionAt = document?.DecisionAt,
		AcceptedExtensions = requirement.AcceptedExtensions,
		MaximumSizeMb = requirement.MaximumSizeMb,
		UploadInstructions = requirement.UploadInstructions,
		RejectionReasons = requirement.RejectionReasons
	};

	public static AdmissionCrmDocumentRequirementDto GetRequirement(AdmissionCrmDocumentChecklistAssignment assignment, string key) =>
		Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(assignment.TemplateSnapshotJson).Requirements
			.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
		?? throw new BusinessException("AdmissionCrm:DocumentRequirementNotFound");

	public static bool IsDocumentComplete(AdmissionCrmApplicantDocumentDto document)
	{
		if (document.Status == AdmissionCrmApplicantDocumentStatus.Waived) return true;
		if (document.RequiresOriginal) return document.Status == AdmissionCrmApplicantDocumentStatus.OriginalSeen;
		return document.Status is AdmissionCrmApplicantDocumentStatus.Verified or AdmissionCrmApplicantDocumentStatus.OriginalSeen;
	}

	public static string GetIncompleteReason(AdmissionCrmApplicantDocumentDto document) => document.Status switch
	{
		null => "missing",
		AdmissionCrmApplicantDocumentStatus.Rejected => "rejected",
		AdmissionCrmApplicantDocumentStatus.Expired => "expired",
		AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview => "awaiting verification",
		AdmissionCrmApplicantDocumentStatus.Verified when document.RequiresOriginal => "original not seen",
		_ => "unresolved"
	};

	public static bool IsRequirementCurrentlyRequired(
		AdmissionCrmDocumentRequirementDto requirement,
		IReadOnlyDictionary<string, JsonElement> responses,
		IReadOnlyDictionary<string, JsonElement> prefill)
	{
		if (requirement.RequirementLevel is AdmissionCrmDocumentRequirementLevel.Required or AdmissionCrmDocumentRequirementLevel.RequiredLater) return true;
		if (requirement.RequirementLevel == AdmissionCrmDocumentRequirementLevel.Optional) return false;
		if (string.IsNullOrWhiteSpace(requirement.ConditionalFieldKey)) return false;
		if (responses.TryGetValue(requirement.ConditionalFieldKey, out var response))
			return string.Equals(JsonText(response), requirement.ConditionalFieldValue, StringComparison.OrdinalIgnoreCase);
		return prefill.TryGetValue(requirement.ConditionalFieldKey, out var value) &&
		       string.Equals(JsonText(value), requirement.ConditionalFieldValue, StringComparison.OrdinalIgnoreCase);
	}

	public static bool HasApplicability(AdmissionCrmPlaybookApplicabilityDto value) => Values(value).Any(x => !string.IsNullOrWhiteSpace(x));

	public static bool MatchesApplicability(AdmissionCrmPlaybookApplicabilityDto configured, AdmissionCrmPlaybookApplicabilityDto actual) =>
		Values(configured).Zip(Values(actual), Matches).All(x => x);

	public static int ApplicabilityScore(AdmissionCrmPlaybookApplicabilityDto value) => Values(value).Count(x => !string.IsNullOrWhiteSpace(x));

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

	public static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions)
		?? throw new BusinessException("AdmissionCrm:InvalidStoredDocumentConfiguration");

	private static string?[] Values(AdmissionCrmPlaybookApplicabilityDto value) =>
		[value.Campus, value.DesiredClass, value.Board, value.Medium, value.ApplicantCategory, value.AdmissionRound, value.AdmissionRoute];

	private static bool Matches(string? configured, string? actual) =>
		string.IsNullOrWhiteSpace(configured) || string.Equals(configured.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);

	private static string JsonText(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
}
