using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmCommunicationOptOutDto
{
	public Guid? LeadId { get; set; }
	public AdmissionCrmMessageChannel Channel { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxEmailLength)]
	public required string ContactValue { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }
}
