namespace ShikshaMitra.Academic.Timetable.Substitutions;

public enum SubstitutionStatus
{
    Pending = 0,
    Assigned = 1,
    WaitingAck = 2,
    Acknowledged = 3,
    Completed = 4,
    Rejected = 5,
    Cancelled = 6,
    Conflict = 7,
    Expired = 8
}

public enum SubstitutionPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum SubstitutionSource
{
    Manual = 0,
    Suggested = 1,
    AutoCreated = 2,
    HrLeave = 3,
    AcademicCalendar = 4,
    Attendance = 5,
    ExamDuty = 6
}

public enum SubstitutionConflictStatus
{
    Clear = 0,
    Info = 1,
    Warning = 2,
    HardBlock = 3
}

public enum SubstitutionAcknowledgementStatus
{
    NotSent = 0,
    Sent = 1,
    Seen = 2,
    Accepted = 3,
    Rejected = 4,
    ChangeRequested = 5,
    Escalated = 6
}

public enum TeacherAvailabilityStatus
{
    Unavailable = 0,
    Leave = 1,
    Training = 2,
    Meeting = 3,
    ExamDuty = 4,
    OtherDuty = 5
}

public enum TeacherAvailabilitySource
{
    Manual = 0,
    HrLeave = 1,
    Attendance = 2,
    AcademicCalendar = 3,
    ExamDuty = 4,
    Event = 5
}
