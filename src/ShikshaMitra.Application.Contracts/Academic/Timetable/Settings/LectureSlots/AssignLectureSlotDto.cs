using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class AssignLectureSlotDto
{
    public List<Guid> SelectedStandardIds { get; set; } = [];
}
