using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmJourneyRuleDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string RuleName { get; set; } = string.Empty;

	public bool IsEnabled { get; set; } = true;
	public int Priority { get; set; } = 100;
	public AdmissionCrmJourneyTrigger Trigger { get; set; } = AdmissionCrmJourneyTrigger.LeadCreated;
	public AdmissionInquiryStage? Stage { get; set; }
	public AdmissionInquiryChannel? Channel { get; set; }
	public Guid? InquirySourceId { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxCampaignLength)]
	public string? CampaignContains { get; set; }

	public AdmissionInquiryStage? LeadStage { get; set; }
	public AdmissionCrmLeadPriority? LeadPriority { get; set; }
	public Guid? OwnerUserId { get; set; }
	public Guid? TargetStandardId { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? DesiredClassContains { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? CampusBranchContains { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? PreferredLanguageContains { get; set; }

	[Range(0, 100)]
	public int? MinimumLeadScore { get; set; }

	[Range(0, 100)]
	public int? MaximumLeadScore { get; set; }

	[Range(0, 3660)]
	public int? DaysSinceLastActivity { get; set; }

	[Range(0, 3660)]
	public int? DaysSinceCreation { get; set; }

	public bool CreateTask { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? TaskTitle { get; set; }

	[Range(0, 720)]
	public int TaskDueOffsetHours { get; set; } = 24;

	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? TaskNotes { get; set; }

	public bool SendMessage { get; set; }
	public Guid? MessageTemplateId { get; set; }
}
