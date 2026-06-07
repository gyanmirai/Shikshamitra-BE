using System;
using ShikshaMitra.HR.Calendar;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;

/// <summary>
/// DTO for calendar view list items (unified events and holidays)
/// </summary>
public class CalendarViewItemDto
{
    /// <summary>
    /// Unique identifier of the calendar item
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title of the event or holiday name
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Type of calendar item: Event or Holiday
    /// </summary>
    public CalendarItemType CalendarItemType { get; set; }

    /// <summary>
    /// Color code for calendar item (from EventType or HolidayType)
    /// </summary>
    public string Color { get; set; } = string.Empty;

    public string TypeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Mode { get; set; }

    public string? Venue { get; set; }

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    public int AudienceCount { get; set; }

    public int Status { get; set; }

    /// <summary>
    /// Start date/time used by calendar UI placement.
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// End date/time used by calendar UI placement.
    /// </summary>
    public DateTimeOffset EndDate { get; set; }
}
