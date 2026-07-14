using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmLeadNoteDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Note { get; set; }

	public bool IsCallNote { get; set; }
}
