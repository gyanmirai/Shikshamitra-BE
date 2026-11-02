using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class Building : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string BuildingName { get; set; } = null!; // Required persisted column; populated by EF or the owning create flow.
	public int TotalFloors { get; set; }
	public string? Description { get; set; }
	public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}
