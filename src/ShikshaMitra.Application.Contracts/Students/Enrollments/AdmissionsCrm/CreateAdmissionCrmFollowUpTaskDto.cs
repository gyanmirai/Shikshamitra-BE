using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmFollowUpTaskDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public required string Title { get; set; }

	public DateTime DueAt { get; set; }
	public Guid? AssignedUserId { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Notes { get; set; }
}
