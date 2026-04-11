namespace ShikshaMitra.HR.Calendar.Events;

/// <summary>
/// Indicates whether a calendar event is conducted online or in-person.
/// </summary>
public enum EventMode
{
    /// <summary>
    /// Virtual / remote event (e.g., video conference).
    /// </summary>
    Online = 0,

    /// <summary>
    /// In-person / physical-venue event.
    /// </summary>
    Offline = 1
}
