using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService
{
    private sealed class RoomMasterRow
    {
        public Guid Id { get; set; }
        public string RoomName { get; set; } = "";
        public DateTime CreationTime { get; set; }
        public int Capacity { get; set; }
        public List<string>? Features { get; set; }
        public string? BuildingName { get; set; }
        public string? FloorName { get; set; }
        public string? TypeName { get; set; }
    }
}
