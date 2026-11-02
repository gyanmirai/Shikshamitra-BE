using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class EducationMediumStageDto : AuditedEntityDto<Guid>
{
    public Guid EducationMediumId { get; set; }
    public string Name { get; set; } = null!; // The response mapper populates these members after construction.
	public int SortOrder { get; set; }
	public byte[]? BannerPicture { get; set; }

	public List<StandardDto> Standards { get; set; } = null!;
}
