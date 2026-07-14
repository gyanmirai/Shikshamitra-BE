using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageTemplateDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string TemplateName { get; set; } = string.Empty;
	public AdmissionCrmMessageChannel Channel { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public string? Subject { get; set; }
	public string Body { get; set; } = string.Empty;
	public IReadOnlyList<string> Variables { get; set; } = Array.Empty<string>();
	public AdmissionCrmMessageTemplateStatus Status { get; set; }
	public bool IsActive { get; set; }
}
