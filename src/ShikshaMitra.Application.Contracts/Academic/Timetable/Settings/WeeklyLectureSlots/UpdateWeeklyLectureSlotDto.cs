using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class UpdateWeeklyLectureSlotDto
{
    public List<WeeklyLectureSubjectDto> Subjects { get; set; } = [];
}
