using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadScoringRulesDto
{
	[Range(0, 100)]
	public int BaseScore { get; set; } = 20;

	[Range(-100, 100)]
	public int BothContactChannelsScore { get; set; } = 10;

	[Range(-100, 100)]
	public int ParentGuardianScore { get; set; } = 10;

	[Range(-100, 100)]
	public int DesiredClassScore { get; set; } = 15;

	[Range(-100, 100)]
	public int CampaignScore { get; set; } = 10;

	[Range(-100, 100)]
	public int ManualBackOfficeChannelScore { get; set; } = 8;

	[Range(-100, 100)]
	public int QuickCallEntryChannelScore { get; set; } = 5;

	[Range(-100, 100)]
	public int WalkInReceptionChannelScore { get; set; } = 15;

	[Range(-100, 100)]
	public int SchoolWebsiteFormChannelScore { get; set; } = 15;

	[Range(-100, 100)]
	public int WhatsAppInquiryChannelScore { get; set; } = 10;

	[Range(-100, 100)]
	public int StaffSharedLinkChannelScore { get; set; } = 12;

	[Range(-100, 100)]
	public int QrCodeScanChannelScore { get; set; } = 10;

	[Range(-100, 100)]
	public int CampaignLinkChannelScore { get; set; } = 12;

	[Range(-100, 100)]
	public int ExamPassScore { get; set; } = 10;

	[Range(-100, 100)]
	public int ExamFailPenalty { get; set; } = -10;

	[Range(-100, 100)]
	public int HighPerformanceScore { get; set; } = 10;

	[Range(0, 100)]
	public int HighPerformancePercentage { get; set; } = 80;

	[Range(1, 100)]
	public int MaxScore { get; set; } = 100;

	public static AdmissionCrmLeadScoringRulesDto CreateDefault()
	{
		return new AdmissionCrmLeadScoringRulesDto();
	}
}
