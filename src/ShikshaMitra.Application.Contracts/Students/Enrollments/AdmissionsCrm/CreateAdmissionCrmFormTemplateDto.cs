using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmFormTemplateDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxTemplateNameLength)]
	public required string TemplateName { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	public AdmissionInquiryChannel Channel { get; set; } = AdmissionInquiryChannel.SchoolWebsiteForm;
	public bool IsPublic { get; set; }
	public bool IsActive { get; set; } = true;

	[MaxLength(AdmissionCrmConsts.MaxConsentTextLength)]
	public string? ConsentText { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxUrlLength)]
	public string? PrivacyNoticeUrl { get; set; }

	[MinLength(1)]
	public IReadOnlyList<AdmissionCrmFormFieldDto> Fields { get; set; } = [];
	public IReadOnlyDictionary<string, AdmissionCrmFormTemplateLocalizationDto> Localizations { get; set; } = new Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>();
}
