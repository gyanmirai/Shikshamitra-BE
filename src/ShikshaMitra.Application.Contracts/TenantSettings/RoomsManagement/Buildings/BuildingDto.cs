using System;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingDto
{
	public Guid Id { get; set; }
	public string BuildingName { get; set; } = null!; // The response mapper populates these members after construction.
	public int TotalFloors { get; set; }
	public string? Description { get; set; }
	public List<FloorDto> Floors { get; set; } = new();
}
