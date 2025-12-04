using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingDto : AuditedEntityDto<Guid>
{
	public string BuildingName { get; set; }
	public int TotalFloors { get; set; }
	public string? Description { get; set; }
}
