using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class PreviewAdmissionCrmMessageTemplateDto
{
	[Required]
	public Guid TemplateId { get; set; }

	public Guid? LeadId { get; set; }

	public Dictionary<string, string> Variables { get; set; } = [];
}
