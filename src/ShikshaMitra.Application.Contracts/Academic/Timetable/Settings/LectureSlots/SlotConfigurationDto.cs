using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class SlotConfigurationDto
{
    public int TotalSlots { get; set; }
    public List<int> WeekRange { get; set; } = [];
    public List<TimeSlotDto> TimeSlots { get; set; } = [];
}

public class TimeSlotDto
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SlotType SlotType { get; set; }
    public BreakType? BreakType { get; set; }
}
