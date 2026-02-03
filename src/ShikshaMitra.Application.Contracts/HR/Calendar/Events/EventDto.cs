using ShikshaMitra.HR.Calendar.EventTypes;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Events;

/// <summary>
/// Data Transfer Object for reading Event data
/// </summary>
public class EventDto : AuditedEntityDto<Guid>
{
    /// <summary>
    /// Tenant ID
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Academic Year ID for filtering events by academic year
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Title of the event
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Event type ID (deprecated - use EventType object instead)
    /// </summary>
    public Guid EventTypeId { get; set; }

    /// <summary>
    /// Full EventType object with name, color, and description
    /// </summary>
    public EventTypeDto? EventType { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Poster URL
    /// </summary>
    public string? Poster { get; set; }

    /// <summary>
    /// Event mode (0=Online, 1=Offline)
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    /// Venue location
    /// </summary>
    public string? Venue { get; set; }

    /// <summary>
    /// Meeting date - only for "Meeting Event" type events, null for other event types
    /// </summary>
    public DateOnly? MeetingDate { get; set; }

    /// <summary>
    /// Start time (HH:mm)
    /// </summary>
    public string StartTime { get; set; }

    /// <summary>
    /// End time (HH:mm)
    /// </summary>
    public string EndTime { get; set; }

    /// <summary>
    /// Is recurring
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

    /// <summary>
    /// Event status (0=Upcoming, 1=Active, 2=Completed)
    /// </summary>
    public int Status { get; set; }
}
