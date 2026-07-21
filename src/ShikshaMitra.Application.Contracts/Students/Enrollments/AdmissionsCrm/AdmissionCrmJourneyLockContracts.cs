using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmPipelineStageDto : UpdateAdmissionCrmPipelineStageDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength)]
	public string StageKey { get; set; } = string.Empty;

	public AdmissionInquiryStage BaseSystemStage { get; set; } = AdmissionInquiryStage.FollowUp;
}

public class AdmissionCrmJourneyLockDto
{
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}
