using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFormTemplateDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string TemplateName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AdmissionInquiryChannel Channel { get; set; }
	public bool IsPublic { get; set; }
	public bool IsActive { get; set; }
	public string? ConsentText { get; set; }
	public string? PrivacyNoticeUrl { get; set; }
	public IReadOnlyList<AdmissionCrmFormFieldDto> Fields { get; set; } = [];
	public IReadOnlyDictionary<string, AdmissionCrmFormTemplateLocalizationDto> Localizations { get; set; } = new Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>();
}

public class AdmissionCrmFormTemplateLocalizationDto
{
	public string TemplateName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ConsentText { get; set; }
}
