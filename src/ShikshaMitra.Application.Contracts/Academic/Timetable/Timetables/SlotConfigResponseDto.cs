using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class SlotConfigResponseDto
{
    public Guid StandardId { get; set; }
    public List<SlotConfigItemDto> Items { get; set; } = [];
}

public class SlotConfigItemDto
{
    public List<string> ApplicableDays { get; set; } = [];
    public List<TimeSlotConfigDto> TimeSlots { get; set; } = [];
}

public class TimeSlotConfigDto
{
    public int Sequence { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public SlotType Type { get; set; }
    public BreakType? BreakType { get; set; }
}
