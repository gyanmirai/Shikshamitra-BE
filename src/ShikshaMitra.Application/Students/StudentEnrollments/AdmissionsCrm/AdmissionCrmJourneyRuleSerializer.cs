using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmJourneyRuleSerializer
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private const int MaxRuleAgeThresholdDays = 3660;

	public static string SerializeRules(IReadOnlyList<AdmissionCrmJourneyRuleDto> rules)
	{
		var normalized = rules
			.OrderBy(x => x.Priority)
			.ThenBy(x => x.RuleName)
			.Select(NormalizeRule)
			.ToList();
		return JsonSerializer.Serialize(normalized, JsonOptions);
	}

	public static List<AdmissionCrmJourneyRuleDto> DeserializeRules(string? rulesJson)
	{
		if (string.IsNullOrWhiteSpace(rulesJson))
		{
			return [];
		}

		return JsonSerializer.Deserialize<List<AdmissionCrmJourneyRuleDto>>(rulesJson, JsonOptions) ?? [];
	}

	private static AdmissionCrmJourneyRuleDto NormalizeRule(AdmissionCrmJourneyRuleDto rule)
	{
		if (string.IsNullOrWhiteSpace(rule.RuleName))
		{
			throw new BusinessException("AdmissionCrm:JourneyRuleNameRequired");
		}

		if (!rule.CreateTask && !rule.SendMessage)
		{
			throw new BusinessException("AdmissionCrm:JourneyRuleActionRequired")
				.WithData("RuleName", rule.RuleName);
		}

		if (rule.Trigger == AdmissionCrmJourneyTrigger.StageReached && !rule.Stage.HasValue)
		{
			throw new BusinessException("AdmissionCrm:JourneyRuleStageRequired")
				.WithData("RuleName", rule.RuleName);
		}

		if (rule.CreateTask && string.IsNullOrWhiteSpace(rule.TaskTitle))
		{
			throw new BusinessException("AdmissionCrm:JourneyTaskTitleRequired")
				.WithData("RuleName", rule.RuleName);
		}

		if (rule.SendMessage && !rule.MessageTemplateId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:JourneyMessageTemplateRequired")
				.WithData("RuleName", rule.RuleName);
		}

		var campaignContains = NormalizeTextCondition(
			rule.CampaignContains,
			AdmissionCrmConsts.MaxCampaignLength,
			"AdmissionCrm:JourneyCampaignConditionTooLong",
			rule.RuleName);
		var desiredClassContains = NormalizeTextCondition(
			rule.DesiredClassContains,
			AdmissionCrmConsts.MaxNameLength,
			"AdmissionCrm:JourneyDesiredClassConditionTooLong",
			rule.RuleName);
		var campusBranchContains = NormalizeTextCondition(
			rule.CampusBranchContains,
			AdmissionCrmConsts.MaxNameLength,
			"AdmissionCrm:JourneyCampusBranchConditionTooLong",
			rule.RuleName);
		var preferredLanguageContains = NormalizeTextCondition(
			rule.PreferredLanguageContains,
			AdmissionCrmConsts.MaxNameLength,
			"AdmissionCrm:JourneyPreferredLanguageConditionTooLong",
			rule.RuleName);

		if (rule.MinimumLeadScore is < 0 or > 100 ||
		    rule.MaximumLeadScore is < 0 or > 100)
		{
			throw new BusinessException("AdmissionCrm:JourneyScoreConditionInvalid")
				.WithData("RuleName", rule.RuleName);
		}

		if (rule.MinimumLeadScore.HasValue &&
		    rule.MaximumLeadScore.HasValue &&
		    rule.MinimumLeadScore.Value > rule.MaximumLeadScore.Value)
		{
			throw new BusinessException("AdmissionCrm:JourneyScoreConditionInvalid")
				.WithData("RuleName", rule.RuleName);
		}

		if (rule.DaysSinceLastActivity is < 0 or > MaxRuleAgeThresholdDays ||
		    rule.DaysSinceCreation is < 0 or > MaxRuleAgeThresholdDays)
		{
			throw new BusinessException("AdmissionCrm:JourneyAgeConditionInvalid")
				.WithData("RuleName", rule.RuleName);
		}

		return new AdmissionCrmJourneyRuleDto
		{
			RuleName = rule.RuleName.Trim(),
			IsEnabled = rule.IsEnabled,
			Priority = rule.Priority,
			Trigger = rule.Trigger,
			Stage = rule.Trigger == AdmissionCrmJourneyTrigger.StageReached ? rule.Stage : null,
			Channel = rule.Channel,
			InquirySourceId = rule.InquirySourceId,
			CampaignContains = campaignContains,
			LeadStage = rule.LeadStage,
			LeadPriority = rule.LeadPriority,
			OwnerUserId = rule.OwnerUserId,
			TargetStandardId = rule.TargetStandardId,
			DesiredClassContains = desiredClassContains,
			CampusBranchContains = campusBranchContains,
			PreferredLanguageContains = preferredLanguageContains,
			MinimumLeadScore = rule.MinimumLeadScore,
			MaximumLeadScore = rule.MaximumLeadScore,
			DaysSinceLastActivity = rule.DaysSinceLastActivity,
			DaysSinceCreation = rule.DaysSinceCreation,
			CreateTask = rule.CreateTask,
			TaskTitle = rule.CreateTask ? rule.TaskTitle?.Trim() : null,
			TaskDueOffsetHours = rule.TaskDueOffsetHours,
			TaskNotes = string.IsNullOrWhiteSpace(rule.TaskNotes) ? null : rule.TaskNotes.Trim(),
			SendMessage = rule.SendMessage,
			MessageTemplateId = rule.SendMessage ? rule.MessageTemplateId : null
		};
	}

	private static string? NormalizeTextCondition(
		string? value,
		int maxLength,
		string errorCode,
		string ruleName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var normalized = value.Trim();
		if (normalized.Length > maxLength)
		{
			throw new BusinessException(errorCode)
				.WithData("RuleName", ruleName);
		}

		return normalized;
	}
}
