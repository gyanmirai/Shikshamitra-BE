namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// Distinguishes between events and holidays on the school calendar.
/// Used by the unified calendar feed to render items with the correct styling and behaviour.
/// </summary>
public enum CalendarItemType
{
    /// <summary>
    /// A scheduled event (e.g., parent-teacher meeting, sports day).
    /// </summary>
    Event = 0,

    /// <summary>
    /// A declared holiday (e.g., national holiday, school closure).
    /// </summary>
    Holiday = 1
}
