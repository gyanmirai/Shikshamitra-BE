namespace ShikshaMitra.HR.Calendar.Events;

/// <summary>
/// Event status enumeration (computed from meeting date)
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// Upcoming event (future date)
    /// </summary>
    Upcoming = 0,

    /// <summary>
    /// Active event (today)
    /// </summary>
    Active = 1,

    /// <summary>
    /// Completed event (past date)
    /// </summary>
    Completed = 2
}
