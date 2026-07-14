using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpdateAdmissionCrmApplicantChecklistProgressDto
{
	public AdmissionCrmApplicantChecklistProgressStatus Status { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Notes { get; set; }
}
