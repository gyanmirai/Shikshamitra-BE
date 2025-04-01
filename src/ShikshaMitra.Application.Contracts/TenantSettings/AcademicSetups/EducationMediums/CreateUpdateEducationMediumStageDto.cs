using System;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class CreateUpdateEducationMediumStageDto
{
	public Guid EducationMediumId { get; set; }
	public string Name { get; set; }

	public byte[]? BannerPicture { get; set; }
}