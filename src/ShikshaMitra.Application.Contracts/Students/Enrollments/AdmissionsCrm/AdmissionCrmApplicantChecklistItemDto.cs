using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantChecklistItemDto
{
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxPurposeLength)]
	public required string Key { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxTimelineTitleLength)]
	public required string Label { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxDescriptionLength)]
	public string? Details { get; set; }

	public bool IsRequired { get; set; } = true;
	public int DisplayOrder { get; set; }
}
