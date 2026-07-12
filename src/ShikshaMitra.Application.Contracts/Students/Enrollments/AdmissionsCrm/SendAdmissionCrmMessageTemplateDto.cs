using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class SendAdmissionCrmMessageTemplateDto
{
	[Required]
	public Guid TemplateId { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxEmailLength)]
	public string? RecipientOverride { get; set; }

	public Dictionary<string, string> Variables { get; set; } = [];
}
