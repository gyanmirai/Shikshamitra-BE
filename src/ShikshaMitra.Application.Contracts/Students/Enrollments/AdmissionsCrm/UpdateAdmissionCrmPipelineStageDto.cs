using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpdateAdmissionCrmPipelineStageDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxStageNameLength)]
	public required string StageName { get; set; }

	[Range(0, 1000)]
	public int DisplayOrder { get; set; }

	public bool IsEnabled { get; set; } = true;

	[MaxLength(AdmissionCrmConsts.MaxStageColorLength)]
	public string? ColorHex { get; set; }

	[Range(0, 720)]
	public int? SlaTargetHours { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxStageOwnerResponsibilityLength)]
	public string? OwnerResponsibility { get; set; }

	public List<AdmissionInquiryStage>? AllowedNextStages { get; set; }
}
