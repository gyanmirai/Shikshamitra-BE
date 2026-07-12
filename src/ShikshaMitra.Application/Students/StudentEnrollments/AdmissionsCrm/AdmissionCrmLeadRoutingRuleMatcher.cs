using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmLeadRoutingRuleMatcher
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static string SerializeSettings(AdmissionCrmLeadRoutingSettingsDto? settings)
	{
		return JsonSerializer.Serialize(NormalizeSettings(settings), JsonOptions);
	}

	public static AdmissionCrmLeadRoutingSettingsDto DeserializeSettings(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson))
		{
			return AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		}

		try
		{
			return NormalizeSettings(
				JsonSerializer.Deserialize<AdmissionCrmLeadRoutingSettingsDto>(settingsJson, JsonOptions));
		}
		catch (JsonException)
		{
			return AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		}
	}

	public static Guid? ResolveOwner(
		AdmissionInquiryLead lead,
		AdmissionCrmLeadRoutingSettingsDto settings,
		IReadOnlyCollection<AdmissionInquiryLead>? existingLeads = null)
	{
		var normalizedSettings = NormalizeSettings(settings);
		if (!normalizedSettings.IsEnabled)
		{
			return null;
		}

		var matchedRule = normalizedSettings.Rules
			.Where(x => x.IsEnabled && x.OwnerUserId.HasValue)
			.OrderBy(x => x.Priority)
			.FirstOrDefault(x => Matches(lead, x));

		if (matchedRule?.OwnerUserId.HasValue == true)
		{
			return matchedRule.OwnerUserId;
		}

		if (normalizedSettings.OwnerPoolUserIds.Count > 0)
		{
			var routedLeads = existingLeads ?? Array.Empty<AdmissionInquiryLead>();
			return normalizedSettings.RoutingStrategy switch
			{
				AdmissionCrmLeadRoutingStrategy.RoundRobin => ResolveRoundRobinOwner(
					normalizedSettings.OwnerPoolUserIds,
					routedLeads),
				AdmissionCrmLeadRoutingStrategy.LeastOpenLeads => ResolveLeastOpenLeadOwner(
					normalizedSettings.OwnerPoolUserIds,
					routedLeads),
				_ => normalizedSettings.DefaultOwnerUserId
			};
		}

		return normalizedSettings.DefaultOwnerUserId;
	}

	private static bool Matches(AdmissionInquiryLead lead, AdmissionCrmLeadRoutingRuleDto rule)
	{
		if (rule.Channel.HasValue && lead.Channel != rule.Channel.Value)
		{
			return false;
		}

		if (rule.InquirySourceId.HasValue && lead.InquirySourceId != rule.InquirySourceId.Value)
		{
			return false;
		}

		if (rule.TargetStandardId.HasValue && lead.TargetStandardId != rule.TargetStandardId.Value)
		{
			return false;
		}

		if (!ContainsIgnoreCase(lead.CampaignName, rule.CampaignContains))
		{
			return false;
		}

		if (!ContainsIgnoreCase(lead.CampusBranchName, rule.CampusBranchContains))
		{
			return false;
		}

		if (!ContainsIgnoreCase(lead.PreferredCommunicationLanguage, rule.PreferredLanguageContains))
		{
			return false;
		}

		if (!ContainsIgnoreCase(lead.DesiredClassName, rule.DesiredClassContains))
		{
			return false;
		}

		return true;
	}

	private static AdmissionCrmLeadRoutingSettingsDto NormalizeSettings(
		AdmissionCrmLeadRoutingSettingsDto? settings)
	{
		if (settings == null)
		{
			return AdmissionCrmLeadRoutingSettingsDto.CreateDefault();
		}

		var normalized = new AdmissionCrmLeadRoutingSettingsDto
		{
			IsEnabled = settings.IsEnabled,
			DefaultOwnerUserId = settings.DefaultOwnerUserId,
			RoutingStrategy = settings.RoutingStrategy,
			OwnerPoolUserIds = (settings.OwnerPoolUserIds ?? [])
				.Where(x => x != Guid.Empty)
				.Distinct()
				.ToList(),
			Rules = (settings.Rules ?? [])
				.Where(x => !string.IsNullOrWhiteSpace(x.RuleName))
				.Select(x => new AdmissionCrmLeadRoutingRuleDto
				{
					RuleName = x.RuleName.Trim(),
					IsEnabled = x.IsEnabled,
					Priority = Math.Clamp(x.Priority, 0, 1000),
					OwnerUserId = x.OwnerUserId,
					Channel = x.Channel,
					InquirySourceId = x.InquirySourceId,
					TargetStandardId = x.TargetStandardId,
					CampaignContains = NormalizeOptional(x.CampaignContains),
					CampusBranchContains = NormalizeOptional(x.CampusBranchContains),
					PreferredLanguageContains = NormalizeOptional(x.PreferredLanguageContains),
					DesiredClassContains = NormalizeOptional(x.DesiredClassContains)
				})
				.OrderBy(x => x.Priority)
				.ToList()
		};

		return normalized;
	}

	private static Guid ResolveRoundRobinOwner(
		IReadOnlyList<Guid> ownerPoolUserIds,
		IReadOnlyCollection<AdmissionInquiryLead> existingLeads)
	{
		var routedLeadCount = existingLeads.Count(x =>
			x.OwnerUserId.HasValue &&
			ownerPoolUserIds.Contains(x.OwnerUserId.Value));
		return ownerPoolUserIds[routedLeadCount % ownerPoolUserIds.Count];
	}

	private static Guid ResolveLeastOpenLeadOwner(
		IReadOnlyCollection<Guid> ownerPoolUserIds,
		IReadOnlyCollection<AdmissionInquiryLead> existingLeads)
	{
		return ownerPoolUserIds
			.OrderBy(ownerUserId => existingLeads.Count(x =>
				x.OwnerUserId == ownerUserId &&
				x.Stage is not AdmissionInquiryStage.Archived
					and not AdmissionInquiryStage.Eligible
					and not AdmissionInquiryStage.Rejected))
			.ThenBy(ownerUserId => existingLeads.Count(x => x.OwnerUserId == ownerUserId))
			.ThenBy(ownerUserId => ownerUserId)
			.First();
	}

	private static bool ContainsIgnoreCase(string? actual, string? expected)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return true;
		}

		return actual?.IndexOf(expected.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string? NormalizeOptional(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
