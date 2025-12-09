using System;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class FloorDto
{
	public Guid Id { get; set; }
	public int FloorNo { get; set; }
	public string FloorName { get; set; }
	public int TotalRoom { get; set; }
}
