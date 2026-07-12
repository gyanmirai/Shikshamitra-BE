using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmLeadScoreCalculator
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static string SerializeRules(AdmissionCrmLeadScoringRulesDto? rules)
	{
		return JsonSerializer.Serialize(NormalizeRules(rules), JsonOptions);
	}

	public static AdmissionCrmLeadScoringRulesDto DeserializeRules(string? rulesJson)
	{
		if (string.IsNullOrWhiteSpace(rulesJson))
		{
			return AdmissionCrmLeadScoringRulesDto.CreateDefault();
		}

		try
		{
			return NormalizeRules(
				JsonSerializer.Deserialize<AdmissionCrmLeadScoringRulesDto>(rulesJson, JsonOptions));
		}
		catch (JsonException)
		{
			return AdmissionCrmLeadScoringRulesDto.CreateDefault();
		}
	}

	public static int Calculate(
		AdmissionInquiryLead lead,
		AdmissionCrmLeadScoringRulesDto rules,
		AdmissionCrmEntranceExamRecord? exam = null)
	{
		var normalizedRules = NormalizeRules(rules);
		var score = normalizedRules.BaseScore;

		if (!string.IsNullOrWhiteSpace(lead.PrimaryContactNo) &&
		    !string.IsNullOrWhiteSpace(lead.Email))
		{
			score += normalizedRules.BothContactChannelsScore;
		}

		if (!string.IsNullOrWhiteSpace(lead.ParentGuardianName))
		{
			score += normalizedRules.ParentGuardianScore;
		}

		if (lead.TargetStandardId.HasValue || !string.IsNullOrWhiteSpace(lead.DesiredClassName))
		{
			score += normalizedRules.DesiredClassScore;
		}

		if (!string.IsNullOrWhiteSpace(lead.CampaignName))
		{
			score += normalizedRules.CampaignScore;
		}

		score += GetChannelScore(lead.Channel, normalizedRules);

		if (exam?.ResultRecordedAt != null)
		{
			score += exam.ExamStatus == EntranceExamStatusEnum.Pass
				? normalizedRules.ExamPassScore
				: normalizedRules.ExamFailPenalty;

			if (exam.Percentage >= normalizedRules.HighPerformancePercentage)
			{
				score += normalizedRules.HighPerformanceScore;
			}
		}

		return Math.Clamp(score, 0, normalizedRules.MaxScore);
	}

	private static AdmissionCrmLeadScoringRulesDto NormalizeRules(AdmissionCrmLeadScoringRulesDto? rules)
	{
		var defaults = AdmissionCrmLeadScoringRulesDto.CreateDefault();
		if (rules == null)
		{
			return defaults;
		}

		return new AdmissionCrmLeadScoringRulesDto
		{
			BaseScore = Clamp(rules.BaseScore, 0, 100, defaults.BaseScore),
			BothContactChannelsScore = Clamp(rules.BothContactChannelsScore, -100, 100, defaults.BothContactChannelsScore),
			ParentGuardianScore = Clamp(rules.ParentGuardianScore, -100, 100, defaults.ParentGuardianScore),
			DesiredClassScore = Clamp(rules.DesiredClassScore, -100, 100, defaults.DesiredClassScore),
			CampaignScore = Clamp(rules.CampaignScore, -100, 100, defaults.CampaignScore),
			ManualBackOfficeChannelScore = Clamp(rules.ManualBackOfficeChannelScore, -100, 100, defaults.ManualBackOfficeChannelScore),
			QuickCallEntryChannelScore = Clamp(rules.QuickCallEntryChannelScore, -100, 100, defaults.QuickCallEntryChannelScore),
			WalkInReceptionChannelScore = Clamp(rules.WalkInReceptionChannelScore, -100, 100, defaults.WalkInReceptionChannelScore),
			SchoolWebsiteFormChannelScore = Clamp(rules.SchoolWebsiteFormChannelScore, -100, 100, defaults.SchoolWebsiteFormChannelScore),
			WhatsAppInquiryChannelScore = Clamp(rules.WhatsAppInquiryChannelScore, -100, 100, defaults.WhatsAppInquiryChannelScore),
			StaffSharedLinkChannelScore = Clamp(rules.StaffSharedLinkChannelScore, -100, 100, defaults.StaffSharedLinkChannelScore),
			QrCodeScanChannelScore = Clamp(rules.QrCodeScanChannelScore, -100, 100, defaults.QrCodeScanChannelScore),
			CampaignLinkChannelScore = Clamp(rules.CampaignLinkChannelScore, -100, 100, defaults.CampaignLinkChannelScore),
			ExamPassScore = Clamp(rules.ExamPassScore, -100, 100, defaults.ExamPassScore),
			ExamFailPenalty = Clamp(rules.ExamFailPenalty, -100, 100, defaults.ExamFailPenalty),
			HighPerformanceScore = Clamp(rules.HighPerformanceScore, -100, 100, defaults.HighPerformanceScore),
			HighPerformancePercentage = Clamp(rules.HighPerformancePercentage, 0, 100, defaults.HighPerformancePercentage),
			MaxScore = Clamp(rules.MaxScore, 1, 100, defaults.MaxScore)
		};
	}

	private static int GetChannelScore(
		AdmissionInquiryChannel channel,
		AdmissionCrmLeadScoringRulesDto rules)
	{
		return channel switch
		{
			AdmissionInquiryChannel.ManualBackOffice => rules.ManualBackOfficeChannelScore,
			AdmissionInquiryChannel.QuickCallEntry => rules.QuickCallEntryChannelScore,
			AdmissionInquiryChannel.WalkInReception => rules.WalkInReceptionChannelScore,
			AdmissionInquiryChannel.SchoolWebsiteForm => rules.SchoolWebsiteFormChannelScore,
			AdmissionInquiryChannel.WhatsAppInquiry => rules.WhatsAppInquiryChannelScore,
			AdmissionInquiryChannel.StaffSharedLink => rules.StaffSharedLinkChannelScore,
			AdmissionInquiryChannel.QrCodeScan => rules.QrCodeScanChannelScore,
			AdmissionInquiryChannel.CampaignLink => rules.CampaignLinkChannelScore,
			_ => rules.ManualBackOfficeChannelScore
		};
	}

	private static int Clamp(int value, int min, int max, int fallback)
	{
		return value < min || value > max ? fallback : value;
	}
}
