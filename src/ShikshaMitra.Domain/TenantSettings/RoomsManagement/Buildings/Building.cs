using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class Building : AuditedAggregateRoot<Guid>
{
	public string BuildingName { get; set; }
	public int TotalFloors { get; set; }
	public string Description { get; set; }
}
