using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class CreateUpdateEducationMediumStageDto
{
	public Guid EducationMediumId { get; set; }
	public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	public byte[]? BannerPicture { get; set; }
}