using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CompleteAdmissionCrmFollowUpTaskDto
{
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? CompletionNotes { get; set; }
}
