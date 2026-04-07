namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Constants for the Student Attendance sub-domain.
/// Mirrors <see cref="ShikshaMitra.HR.AttendanceAndLeaves.Settings.LeaveConsts"/>.
/// </summary>
public static class AttendanceConsts
{
    /// <summary>Maximum length for attendance remarks.</summary>
    public const int MaxRemarksLength = 500;

    /// <summary>Maximum number of entries allowed per AttendanceDay.</summary>
    public const int MaxEntriesPerDay = 1000;

    /// <summary>Table name prefix for attendance entities.</summary>
    public const string TablePrefix = "Sm";

    /// <summary>DB schema for attendance tables.</summary>
    public const string? DbSchema = null;
}
