using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmMessageTemplateDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxTemplateNameLength)]
	public required string TemplateName { get; set; }

	public AdmissionCrmMessageChannel Channel { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxPurposeLength)]
	public required string Purpose { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxMessageSubjectLength)]
	public string? Subject { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxMessageBodyLength)]
	public required string Body { get; set; }

	public IReadOnlyList<string> Variables { get; set; } = [];
}
