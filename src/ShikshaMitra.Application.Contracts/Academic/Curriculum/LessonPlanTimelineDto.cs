using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanTimelineDto
{
    public DateTime Date { get; set; }
    public Guid TeacherId { get; set; }
    public List<TimelineSlotDto> Slots { get; set; } = new();
}
