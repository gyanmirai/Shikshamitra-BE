namespace ShikshaMitra.Academic.Calendar;

public enum DayType
{
    WorkingDay = 1,
    Holiday = 2,
    Vacation = 3,
    HalfDay = 4,
    Event = 5,
    Exam = 6,
    PTM = 7,
    EmergencyClosure = 8
}

public enum HolidayCategory
{
    National = 1,
    Regional = 2,
    Festival = 3,
    Management = 4,
    WeatherEmergency = 5,
    GovernmentOrder = 6,
    Other = 7
}

public enum CalendarStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public enum CalendarDayStatus
{
    Active = 1,
    Cancelled = 2,
    Rescheduled = 3
}

public enum SchoolShiftType
{
    FullDay = 1,
    Morning = 2,
    Afternoon = 3,
    Evening = 4
}

public enum CalendarImpactType
{
    TimetableSlot = 1,
    HomeworkDueDate = 2,
    AssignmentDueDate = 3,
    ExamSchedule = 4,
    OnlineClass = 5,
    AttendanceSession = 6,
    LessonPlan = 7,
    TeacherDiary = 8,
    PTMBooking = 9,
    EventSchedule = 10
}

public enum CalendarImpactStatus
{
    Pending = 1,
    Suggested = 2,
    Rescheduled = 3,
    IgnoredWithReason = 4,
    Resolved = 5
}

public enum CalendarAuditAction
{
    CalendarCreated = 1,
    CalendarUpdated = 2,
    CalendarPublished = 3,
    CalendarArchived = 4,
    DayAdded = 5,
    DayUpdated = 6,
    DayDeleted = 7,
    EmergencyClosureDeclared = 8,
    ImpactResolved = 9,
    OverrideApplied = 10,
    CalendarDeleted = 11,
    WorkingHourAdded = 12,
    WorkingHourUpdated = 13,
    WorkingHourDeleted = 14,
    SettingsUpdated = 15
}

/// <summary>Channel/actor type that produced an audit event. Mirrors the FE's AuditSource enum.</summary>
public enum CalendarAuditSource
{
    Web = 1,
    Mobile = 2,
    API = 3,
    BulkUpload = 4,
    SystemJob = 5
}

public enum WeekendRule
{
    SundayOnly = 1,
    SaturdaySunday = 2,
    AlternateSaturday = 3,
    Custom = 4
}

public enum RescheduleMode
{
    ManualReviewOnly = 1,
    SuggestNewDate = 2,
    AutoReschedule = 3
}

public enum CalendarVisibility
{
    InternalOnly = 1,
    TeachersOnly = 2,
    StaffAndStudents = 3,
    ParentsAndStudents = 4,
    Public = 5
}
