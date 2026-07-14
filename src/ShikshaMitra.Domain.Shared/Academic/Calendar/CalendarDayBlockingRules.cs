namespace ShikshaMitra.Academic.Calendar;

/// <summary>
/// Conflict/blocking behaviour is derived from the day type instead of
/// per-day flags, so markers no longer carry individual blocking switches.
/// </summary>
public static class CalendarDayBlockingRules
{
    public static bool BlocksTimetable(DayType dayType) =>
        dayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure;

    public static bool BlocksHomeworkDueDate(DayType dayType) =>
        dayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure;

    public static bool BlocksExamSchedule(DayType dayType) =>
        dayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure;

    public static bool BlocksLessonPlan(DayType dayType) =>
        dayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure;

    public static bool BlocksOnlineClass(DayType dayType) =>
        dayType is DayType.Holiday or DayType.EmergencyClosure;

    public static bool ExemptAttendance(DayType dayType) =>
        dayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure;

    public static bool Blocks(DayType dayType, string validationType) =>
        validationType?.ToLowerInvariant() switch
        {
            "timetable" => BlocksTimetable(dayType),
            "homeworkduedate" or "assignmentduedate" => BlocksHomeworkDueDate(dayType),
            "examschedule" => BlocksExamSchedule(dayType),
            "lessonplan" => BlocksLessonPlan(dayType),
            "onlineclass" => BlocksOnlineClass(dayType),
            "attendance" => ExemptAttendance(dayType),
            _ => false
        };
}
