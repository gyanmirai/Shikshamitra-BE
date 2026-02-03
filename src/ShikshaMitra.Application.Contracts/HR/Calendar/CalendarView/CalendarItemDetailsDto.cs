using System;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;

/// <summary>
/// DTO for calendar item popup details (event or holiday)
/// </summary>
public class CalendarItemDetailsDto
{
    /// <summary>
    /// Unique identifier of the calendar item
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of calendar item: Event or Holiday
    /// </summary>
    public CalendarItemType CalendarItemType { get; set; }

    /// <summary>
    /// Status of the item: 0=Upcoming, 1=Active/Ongoing, 2=Completed
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Title of the event or holiday name
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Date of the event/holiday (for events: MeetingDate, for holidays: StartDate)
    /// </summary>
    public DateTimeOffset? Date { get; set; }

    /// <summary>
    /// Time details for the calendar item
    /// </summary>
    public CalendarItemTimeDto? Time { get; set; }

    /// <summary>
    /// Number of audience members targeted
    /// </summary>
    public int? AudienceCount { get; set; }

    /// <summary>
    /// Event type details (only for events)
    /// </summary>
    public EventTypeDto? EventType { get; set; }

    /// <summary>
    /// Holiday type details (only for holidays)
    /// </summary>
    public HolidayTypeDto? HolidayType { get; set; }

    /// <summary>
    /// Event mode: Online or Offline (only for events)
    /// </summary>
    public EventMode? Mode { get; set; }

    /// <summary>
    /// Venue details
    /// </summary>
    public CalendarVenueDto? Venue { get; set; }

    /// <summary>
    /// Description of the event or holiday
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// DTO for calendar item time details
/// </summary>
public class CalendarItemTimeDto
{
    /// <summary>
    /// Start time in HH:mm format
    /// </summary>
    public string? StartTime { get; set; }

    /// <summary>
    /// End time in HH:mm format
    /// </summary>
    public string? EndTime { get; set; }
}

/// <summary>
/// DTO for venue details
/// </summary>
public class CalendarVenueDto
{
    /// <summary>
    /// Venue type: ONLINE or OFFLINE
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Meeting link for online events
    /// </summary>
    public string? MeetingLink { get; set; }

    /// <summary>
    /// Physical address for offline events
    /// </summary>
    public string? Address { get; set; }
}
