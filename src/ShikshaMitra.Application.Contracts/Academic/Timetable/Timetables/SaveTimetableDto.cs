using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class SaveTimetableDto
{
    public TimetableStatus Status { get; set; }
    public Dictionary<TimetableDayOfWeek, List<TimetableSlotSaveDto>> Timetable { get; set; } = [];
}

public class TimetableSlotSaveDto
{
    public int Sequence { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public SlotType Type { get; set; }
    public BreakType? BreakType { get; set; }
    public Curriculum.SubjectType? SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public List<TimetableSubjectSaveDto> Subjects { get; set; } = [];
}

public class TimetableSubjectSaveDto
{
    public Guid SubjectId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? RoomId { get; set; }
}
