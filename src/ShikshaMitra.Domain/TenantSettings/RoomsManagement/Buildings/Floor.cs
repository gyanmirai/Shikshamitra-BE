using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class Floor : Entity<Guid>
{
	public string FloorName { get; set; }
	public int TotalRoom { get; set; }
	public Guid BuildingId { get; set; }
	public Building Building { get; set; }
	public int FloorNo { get; set; }
}
