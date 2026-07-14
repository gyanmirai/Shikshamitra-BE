using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageTemplatePreviewDto
{
	public Guid TemplateId { get; set; }
	public Guid? LeadId { get; set; }
	public AdmissionCrmMessageChannel Channel { get; set; }
	public string? RenderedSubject { get; set; }
	public string RenderedBody { get; set; } = string.Empty;
	public IReadOnlyDictionary<string, string> ResolvedVariables { get; set; } =
		new Dictionary<string, string>();
	public IReadOnlyList<string> UnresolvedVariables { get; set; } = Array.Empty<string>();
}
