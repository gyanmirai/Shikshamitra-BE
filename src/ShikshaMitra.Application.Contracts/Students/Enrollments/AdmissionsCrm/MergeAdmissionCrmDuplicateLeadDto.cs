using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class MergeAdmissionCrmDuplicateLeadDto
{
	[Required]
	public Guid DuplicateLeadId { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}
