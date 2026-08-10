using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmCycleConfigurationDto Cycle(AdmissionCrmCycleConfiguration value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, CycleName = value.CycleName, IsActive = value.IsActive,
		RegistrationFormRequiredBeforeExam = value.RegistrationFormRequiredBeforeExam, EntranceExamRequired = value.EntranceExamRequired,
		JourneyLocked = value.JourneyLocked, JourneyLockedAt = value.JourneyLockedAt,
		JourneyLockedByUserId = value.JourneyLockedByUserId, JourneyLockReason = value.JourneyLockReason,
		StaleAfterDays = value.StaleAfterDays, ArchiveAfterDays = value.ArchiveAfterDays,
		DefaultLeadSlaHours = value.DefaultLeadSlaHours, ActivationCancellationWindowDays = value.ActivationCancellationWindowDays,
		DefaultExamPassingPercentage = value.DefaultExamPassingPercentage, MeritRankingMode = value.MeritRankingMode,
		ExamReadinessFields = value.ExamReadinessFields,
		LeadScoringRules = AdmissionCrmLeadScoreCalculator.DeserializeRules(value.LeadScoringRulesJson),
		LeadRoutingSettings = AdmissionCrmLeadRoutingRuleMatcher.DeserializeSettings(value.LeadRoutingRulesJson),
		JourneyRules = AdmissionCrmJourneyRuleSerializer.DeserializeRules(value.JourneyRulesJson),
		ApplicantChecklist = AdmissionCrmApplicantChecklistSerializer.DeserializeItems(value.ApplicantChecklistJson),
		ChannelSettings = AdmissionCrmChannelSettingsSerializer.DeserializeSettings(value.ChannelSettingsJson),
		SlaPolicy = JsonSerializer.Deserialize<AdmissionCrmSlaPolicyDto>(value.SlaPolicyJson) ?? new AdmissionCrmSlaPolicyDto(),
		Notes = value.Notes
	};

	public static AdmissionCrmPipelineStageDto PipelineStage(AdmissionCrmPipelineStage value, bool locked) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, SystemStage = value.SystemStage, StageKey = value.StageKey,
		BaseSystemStage = value.BaseSystemStage, StageName = value.StageName, DisplayOrder = value.DisplayOrder,
		IsEnabled = value.IsEnabled, IsSystem = value.IsSystem, StructureLocked = locked, ColorHex = value.ColorHex,
		SlaTargetHours = value.SlaTargetHours, OwnerResponsibility = value.OwnerResponsibility,
		AllowedNextStages = DeserializeAllowedNextStages(value.AllowedNextStagesJson).ToList()
	};

	public static AdmissionCrmFormTemplateDto FormTemplate(AdmissionInquiryFormTemplate value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, TemplateName = value.TemplateName,
		Description = value.Description, Channel = value.Channel, IsPublic = value.IsPublic, IsActive = value.IsActive,
		ConsentText = value.ConsentText, PrivacyNoticeUrl = value.PrivacyNoticeUrl, Fields = DeserializeFields(value.FieldsJson),
		Localizations = JsonSerializer.Deserialize<Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>>(value.LocalizationsJson)
			?? new Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>()
	};

	public static AdmissionCrmMessageTemplateDto MessageTemplate(AdmissionCrmMessageTemplate value) => new()
	{
		Id = value.Id, AcademicYearId = value.AcademicYearId, TemplateName = value.TemplateName, Channel = value.Channel,
		Purpose = value.Purpose, Subject = value.Subject, Body = value.Body, Variables = DeserializeVariables(value.VariablesJson),
		Status = value.Status, IsActive = value.IsActive
	};

	public static string SerializeFields(IReadOnlyList<AdmissionCrmFormFieldDto> fields)
	{
		if (fields.Count == 0) throw new BusinessException("AdmissionCrm:FormFieldsRequired");
		var duplicate = fields.GroupBy(x => x.Key.Trim().ToLowerInvariant()).FirstOrDefault(x => x.Count() > 1);
		if (duplicate != null) throw new BusinessException("AdmissionCrm:DuplicateFormFieldKey").WithData("FieldKey", duplicate.Key);
		return JsonSerializer.Serialize(fields.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key).ToList(), JsonOptions);
	}

	public static string SerializeVariables(IReadOnlyList<string> variables)
	{
		var normalized = variables.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x).ToList();
		var duplicate = normalized.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
		if (duplicate != null) throw new BusinessException("AdmissionCrm:DuplicateMessageTemplateVariable").WithData("Variable", duplicate.Key);
		return JsonSerializer.Serialize(normalized, JsonOptions);
	}

	public static string? SerializeAllowedNextStages(IEnumerable<AdmissionInquiryStage>? values)
	{
		if (values == null) return null;
		return JsonSerializer.Serialize(values.Distinct().ToList(), JsonOptions);
	}

	public static IReadOnlyList<AdmissionCrmDefaultPipelineStage> DefaultPipelineStages() =>
	[
		new(AdmissionInquiryStage.NewInquiry, "New Inquiry", 10, "#1976D2", 24, "Admissions counselor",
			[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.ExamReady, AdmissionInquiryStage.Stale, AdmissionInquiryStage.Archived]),
		new(AdmissionInquiryStage.FollowUp, "Follow-up", 20, "#0288D1", 24, "Assigned lead owner",
			[AdmissionInquiryStage.ExamReady, AdmissionInquiryStage.Stale, AdmissionInquiryStage.Archived]),
		new(AdmissionInquiryStage.ExamReady, "Exam Ready", 30, "#2E7D32", 12, "Admissions coordinator",
			[AdmissionInquiryStage.ExamScheduled, AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.Archived]),
		new(AdmissionInquiryStage.ExamScheduled, "Exam Scheduled", 40, "#6A1B9A", null, "Exam coordinator",
			[AdmissionInquiryStage.ExamCompleted, AdmissionInquiryStage.FollowUp]),
		new(AdmissionInquiryStage.ExamCompleted, "Exam Completed", 50, "#455A64", null, "Result reviewer",
			[AdmissionInquiryStage.Eligible, AdmissionInquiryStage.Waitlisted, AdmissionInquiryStage.Rejected]),
		new(AdmissionInquiryStage.Eligible, "Eligible", 60, "#2E7D32", null, "Admissions decision owner", []),
		new(AdmissionInquiryStage.Waitlisted, "Waitlisted", 70, "#ED6C02", null, "Admissions decision owner",
			[AdmissionInquiryStage.Eligible, AdmissionInquiryStage.Rejected, AdmissionInquiryStage.Archived]),
		new(AdmissionInquiryStage.Rejected, "Rejected", 80, "#D32F2F", null, "Admissions decision owner", [AdmissionInquiryStage.Archived]),
		new(AdmissionInquiryStage.Stale, "Stale", 90, "#757575", null, "Assigned lead owner",
			[AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.Archived]),
		new(AdmissionInquiryStage.Archived, "Archived", 100, "#424242", null, "Admissions administrator", [])
	];

	public static void ValidateSlaPolicy(AdmissionCrmSlaPolicyDto policy)
	{
		if (policy.WorkingDays.Count == 0 || policy.WorkingDays.Count > 7 ||
			policy.WorkingDays.Distinct().Count() != policy.WorkingDays.Count || policy.WorkingDays.Any(day => !Enum.IsDefined(day)))
			throw new BusinessException("AdmissionCrm:InvalidSlaWorkingDays");
		if (policy.WorkingDayStartHour is < 0 or > 23 || policy.WorkingDayEndHour is < 1 or > 24 ||
			policy.WorkingDayEndHour <= policy.WorkingDayStartHour) throw new BusinessException("AdmissionCrm:InvalidSlaWorkingHours");
		if (policy.EscalationAfterHours is < 0 or > 720 || policy.Holidays.Count > 366)
			throw new BusinessException("AdmissionCrm:InvalidSlaEscalationPolicy");
		try { _ = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId); }
		catch (TimeZoneNotFoundException) { throw new BusinessException("AdmissionCrm:InvalidSlaTimeZone"); }
		catch (InvalidTimeZoneException) { throw new BusinessException("AdmissionCrm:InvalidSlaTimeZone"); }
	}

	private static IReadOnlyList<AdmissionCrmFormFieldDto> DeserializeFields(string json) =>
		JsonSerializer.Deserialize<List<AdmissionCrmFormFieldDto>>(json, JsonOptions) ?? [];
	private static IReadOnlyList<string> DeserializeVariables(string json) =>
		JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
	private static IReadOnlyList<AdmissionInquiryStage> DeserializeAllowedNextStages(string? json) => string.IsNullOrWhiteSpace(json)
		? [] : JsonSerializer.Deserialize<List<AdmissionInquiryStage>>(json, JsonOptions) ?? [];
}

internal sealed record AdmissionCrmDefaultPipelineStage(AdmissionInquiryStage SystemStage, string StageName,
	int DisplayOrder, string ColorHex, int? SlaTargetHours, string OwnerResponsibility,
	IReadOnlyList<AdmissionInquiryStage> AllowedNextStages);
