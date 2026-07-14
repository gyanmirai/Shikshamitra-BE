using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class ReopenAdmissionCrmLeadDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}
