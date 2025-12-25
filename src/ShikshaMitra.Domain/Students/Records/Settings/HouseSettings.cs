using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records.Settings;

public class HouseSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string Name { get; set; }
	public string Color { get; set; }
	public string? LogoUrl { get; set; }
	public string? Description { get; set; }
	public Guid AcademicMediumId { get; set; }

	protected HouseSettings()
	{
	}

	public HouseSettings(
		Guid id,
		string name,
		string color,
		Guid academicMediumId,
		string? logoUrl = null,
		string? description = null
	) : base(id)
	{
		Name = name;
		Color = color;
		AcademicMediumId = academicMediumId;
		LogoUrl = logoUrl;
		Description = description;
	}

	public void UpdateDetails(
		string name,
		string color,
		string? logoUrl = null,
		string? description = null
	)
	{
		Name = name;
		Color = color;
		LogoUrl = logoUrl;
		Description = description;
	}
}
