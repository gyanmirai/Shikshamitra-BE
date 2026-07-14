using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadRoutingRuleDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string RuleName { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;

	[Range(0, 1000)]
	public int Priority { get; set; }

	public Guid? OwnerUserId { get; set; }

	public AdmissionInquiryChannel? Channel { get; set; }

	public Guid? InquirySourceId { get; set; }

	public Guid? TargetStandardId { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxCampaignLength)]
	public string? CampaignContains { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? CampusBranchContains { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? PreferredLanguageContains { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? DesiredClassContains { get; set; }
}
