namespace ShikshaMitra.HR.Calendar.Holidays;

/// <summary>
/// Represents the status of a holiday based on current date
/// </summary>
public enum HolidayStatus
{
    /// <summary>
    /// Holiday is in the future
    /// </summary>
    Upcoming = 0,

    /// <summary>
    /// Holiday is currently ongoing
    /// </summary>
    Ongoing = 1,

    /// <summary>
    /// Holiday has already passed
    /// </summary>
    Completed = 2
}
