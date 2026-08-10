using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmJourneyRuleMatcher
{
	internal static bool JourneyRuleMatchesLead(
		AdmissionCrmJourneyRuleDto rule,
		AdmissionInquiryLead lead,
		RunAdmissionCrmJourneyRulesDto input,
		DateTimeOffset now)
	{
		if (rule.Trigger == AdmissionCrmJourneyTrigger.StageReached &&
			rule.Stage.HasValue &&
			lead.Stage != rule.Stage.Value)
		{
			return false;
		}

		if (input.Stage.HasValue && lead.Stage != input.Stage.Value)
		{
			return false;
		}

		if (rule.Channel.HasValue && lead.Channel != rule.Channel.Value)
		{
			return false;
		}

		if (rule.InquirySourceId.HasValue && lead.InquirySourceId != rule.InquirySourceId.Value)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(rule.CampaignContains) &&
			(string.IsNullOrWhiteSpace(lead.CampaignName) ||
			 !lead.CampaignName.Contains(rule.CampaignContains, StringComparison.OrdinalIgnoreCase)))
		{
			return false;
		}

		if (rule.LeadStage.HasValue && lead.Stage != rule.LeadStage.Value)
		{
			return false;
		}

		if (rule.LeadPriority.HasValue && lead.Priority != rule.LeadPriority.Value)
		{
			return false;
		}

		if (rule.OwnerUserId.HasValue && lead.OwnerUserId != rule.OwnerUserId.Value)
		{
			return false;
		}

		if (rule.TargetStandardId.HasValue && lead.TargetStandardId != rule.TargetStandardId.Value)
		{
			return false;
		}

		if (!TextConditionMatches(lead.DesiredClassName, rule.DesiredClassContains) ||
			!TextConditionMatches(lead.CampusBranchName, rule.CampusBranchContains) ||
			!TextConditionMatches(lead.PreferredCommunicationLanguage, rule.PreferredLanguageContains))
		{
			return false;
		}

		if (rule.MinimumLeadScore.HasValue && lead.LeadScore < rule.MinimumLeadScore.Value)
		{
			return false;
		}

		if (rule.MaximumLeadScore.HasValue && lead.LeadScore > rule.MaximumLeadScore.Value)
		{
			return false;
		}

		if (rule.DaysSinceLastActivity.HasValue &&
			lead.LastActivityTime > now.AddDays(-rule.DaysSinceLastActivity.Value))
		{
			return false;
		}

		if (rule.DaysSinceCreation.HasValue &&
			new DateTimeOffset(DateTime.SpecifyKind(lead.CreationTime, DateTimeKind.Utc)) >
			now.AddDays(-rule.DaysSinceCreation.Value))
		{
			return false;
		}

		return true;
	}

	internal static bool TextConditionMatches(string? value, string? contains)
	{
		return string.IsNullOrWhiteSpace(contains) ||
			   (!string.IsNullOrWhiteSpace(value) &&
				value.Contains(contains, StringComparison.OrdinalIgnoreCase));
	}
}