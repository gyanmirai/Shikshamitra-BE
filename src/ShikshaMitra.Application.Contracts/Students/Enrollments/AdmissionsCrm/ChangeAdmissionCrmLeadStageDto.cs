using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class ChangeAdmissionCrmLeadStageDto
{
	public AdmissionInquiryStage Stage { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }

	public bool OverrideWorkflowGuards { get; set; }
}
