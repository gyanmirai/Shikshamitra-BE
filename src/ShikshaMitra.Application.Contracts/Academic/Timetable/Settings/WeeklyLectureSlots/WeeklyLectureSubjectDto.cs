using System;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class WeeklyLectureSubjectDto
{
    public Guid? SubjectId { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public int WeeklyLectures { get; set; }
}
