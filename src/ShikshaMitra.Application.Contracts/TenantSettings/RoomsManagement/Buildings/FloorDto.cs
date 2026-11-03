using System;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class FloorDto
{
	public Guid Id { get; set; }
	public int FloorNo { get; set; }
	public string FloorName { get; set; } = null!; // The response mapper populates these members after construction.
	public int TotalRoom { get; set; }
	public int RoomCount { get; set; }
}
