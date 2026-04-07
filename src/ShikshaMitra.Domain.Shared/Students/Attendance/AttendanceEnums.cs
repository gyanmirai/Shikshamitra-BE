namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Shared enums for the Student Attendance sub-domain.
/// </summary>

/// <summary>
/// Represents the half-day session for attendance.
/// </summary>
public enum AttendanceSession
{
    Morning   = 0,
    Afternoon = 1,
}

/// <summary>
/// Represents the lock state of an AttendanceDay.
/// </summary>
public enum AttendanceDayLockState
{
    Open   = 0,
    Locked = 1,
}
