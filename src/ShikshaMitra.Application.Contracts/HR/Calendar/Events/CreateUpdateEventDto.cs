using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Calendar.Events;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Events;

/// <summary>
/// DTO for creating and updating events
/// </summary>
public class CreateUpdateEventDto
{
    /// <summary>
    /// Academic Year ID for filtering events by academic year (required)
    /// UUID format: 550e8400-e29b-41d4-a716-446655440000
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Title of the event (1-256 characters, required)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the EventType, required
    /// </summary>
    public Guid EventTypeId { get; set; }

    /// <summary>
    /// Optional description (0-500 characters)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional poster URL
    /// </summary>
    public string? Poster { get; set; }

    /// <summary>
    /// Event mode: Online or Offline
    /// </summary>
    public EventMode Mode { get; set; } = EventMode.Offline;

    /// <summary>
    /// Venue location (required for offline events)
    /// </summary>
    public string? Venue { get; set; }

    /// <summary>
    /// Meeting date - only required for "Meeting Event" type events.
    /// For other event types, this should not be provided.
    /// </summary>
    public DateTimeOffset? MeetingDate { get; set; }

    /// <summary>
    /// Start time in HH:mm format, required
    /// </summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// End time in HH:mm format, required
    /// </summary>
    public string EndTime { get; set; } = string.Empty;

    /// <summary>
    /// Whether event is recurring
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// Recurrence configuration (JSON)
    /// </summary>
    public string? RecurrenceJson { get; set; }

    /// <summary>
    /// List of user/group IDs this event applies to
    /// </summary>
    public List<Guid> AudienceIds { get; set; } = new();

}
