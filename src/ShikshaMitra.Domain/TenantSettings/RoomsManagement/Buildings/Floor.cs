using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class Floor : Entity<int>
{
	public string FloorName { get; set; }
	public int TotalRoom { get; set; }
	public Guid BuildingId { get; set; }
	public Building Building { get; set; }
}
