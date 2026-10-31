using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records.Settings;

public class HouseSettingsDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.
	public string Color { get; set; } = null!;
	public string? LogoUrl { get; set; }
	public string? Description { get; set; }
	public Guid AcademicMediumId { get; set; }
}
