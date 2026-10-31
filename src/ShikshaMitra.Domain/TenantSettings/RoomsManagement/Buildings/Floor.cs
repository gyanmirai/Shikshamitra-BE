using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class Floor : Entity<Guid>
{
	public string FloorName { get; set; } = null!; // Required persisted column; populated by EF or the owning create flow.
	public int TotalRoom { get; set; }
	public Guid BuildingId { get; set; }
	public Building Building { get; set; } = null!; // Populated by EF relationship materialization.
	public int FloorNo { get; set; }
}
