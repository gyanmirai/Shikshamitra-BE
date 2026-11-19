using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomBuildingLookupDto
{
	public Guid Id { get; set; }
	public string BuildingName { get; set; } = string.Empty;
	public List<RoomFloorLookupDto> Floors { get; set; } = [];
}

public class RoomFloorLookupDto
{
	public Guid Id { get; set; }
	public string FloorName { get; set; } = string.Empty;
	public int FloorNo { get; set; }
}
