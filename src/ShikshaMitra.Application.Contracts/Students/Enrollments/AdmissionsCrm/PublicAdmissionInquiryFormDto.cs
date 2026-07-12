using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class PublicAdmissionInquiryFormDto
{
	public Guid TenantId { get; set; }
	public Guid TemplateId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string TemplateName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AdmissionInquiryChannel Channel { get; set; }
	public string ConsentText { get; set; } = string.Empty;
	public string PrivacyNoticeUrl { get; set; } = string.Empty;
	public string ConsentVersion { get; set; } = string.Empty;
	public IReadOnlyList<AdmissionCrmFormFieldDto> Fields { get; set; } = [];
}
