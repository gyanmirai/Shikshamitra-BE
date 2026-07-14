using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class SuggestedRoom
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; }
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public int Capacity { get; set; }
    public List<string> Features { get; set; } = new();
    public int MatchScore { get; set; }
    public bool NeedsApproval { get; set; }
    public string ConflictStatus { get; set; } = "none";
    public List<string> Badges { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}
