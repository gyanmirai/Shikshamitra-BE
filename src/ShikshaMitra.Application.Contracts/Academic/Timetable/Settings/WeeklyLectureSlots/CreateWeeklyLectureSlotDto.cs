using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class CreateWeeklyLectureSlotDto
{
    public Guid StandardId { get; set; }
    public List<WeeklyLectureSubjectDto> Subjects { get; set; } = [];
}
