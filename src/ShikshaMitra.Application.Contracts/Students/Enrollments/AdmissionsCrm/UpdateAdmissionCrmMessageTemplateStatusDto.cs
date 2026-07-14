namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpdateAdmissionCrmMessageTemplateStatusDto
{
	public AdmissionCrmMessageTemplateStatus Status { get; set; }
	public bool IsActive { get; set; } = true;
}
