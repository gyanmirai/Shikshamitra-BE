using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Calendar.Events;

/// <summary>
/// Represents a calendar event with temporal properties and recurrence support.
/// Tenant-specific, supporting different events per tenant.
/// </summary>
public class Event : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Title/name of the event
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Reference to EventType for categorization
    /// </summary>
    public Guid EventTypeId { get; set; }

    /// <summary>
    /// Optional description of the event
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Poster/image URL for the event
    /// </summary>
    public string? Poster { get; set; }

    /// <summary>
    /// Event mode: ONLINE or OFFLINE
    /// </summary>
    public EventMode Mode { get; set; } = EventMode.Offline;

    /// <summary>
    /// Venue location (required for offline events)
    /// </summary>
    public string? Venue { get; set; }

    /// <summary>
    /// Meeting/event date
    /// </summary>
    public DateOnly MeetingDate { get; set; }

    /// <summary>
    /// Start time (HH:mm format)
    /// </summary>
    public string StartTime { get; set; }

    /// <summary>
    /// End time (HH:mm format)
    /// </summary>
    public string EndTime { get; set; }

    /// <summary>
    /// Whether this is a recurring event
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// Recurrence configuration (if IsRecurring is true)
    /// </summary>
    public string? RecurrenceJson { get; set; } // Stores recurrence configuration as JSON

    /// <summary>
    /// List of user/group IDs this event applies to
    /// </summary>
    public List<Guid> AudienceIds { get; set; } = new();

    /// <summary>
    /// Computed status based on meeting date
    /// </summary>
    public EventStatus Status
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (MeetingDate < today)
                return EventStatus.Completed;

            if (MeetingDate == today)
                return EventStatus.Active;

            return EventStatus.Upcoming;
        }
    }

    protected Event() { }

    /// <summary>
    /// Factory method to create an event
    /// </summary>
    public static Event Create(
        Guid? tenantId,
        string title,
        Guid eventTypeId,
        DateOnly meetingDate,
        string startTime,
        string endTime,
        string? description = null,
        string? poster = null,
        EventMode mode = EventMode.Offline,
        string? venue = null,
        bool isRecurring = false,
        string? recurrenceJson = null,
        List<Guid>? audienceIds = null)
    {
        ValidateDateAndTime(meetingDate, startTime, endTime);

        return new Event
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            EventTypeId = eventTypeId,
            MeetingDate = meetingDate,
            StartTime = startTime,
            EndTime = endTime,
            Description = description,
            Poster = poster,
            Mode = mode,
            Venue = venue,
            IsRecurring = isRecurring,
            RecurrenceJson = recurrenceJson,
            AudienceIds = audienceIds ?? new()
        };
    }

    /// <summary>
    /// Update event details
    /// </summary>
    public void Update(
        string title,
        DateOnly meetingDate,
        string startTime,
        string endTime,
        string? description = null,
        string? poster = null,
        EventMode? mode = null,
        string? venue = null,
        bool? isRecurring = null,
        string? recurrenceJson = null,
        List<Guid>? audienceIds = null)
    {
        ValidateDateAndTime(meetingDate, startTime, endTime);

        Title = title;
        MeetingDate = meetingDate;
        StartTime = startTime;
        EndTime = endTime;

        if (description != null)
            Description = description;

        if (poster != null)
            Poster = poster;

        if (mode.HasValue)
            Mode = mode.Value;

        if (venue != null)
            Venue = venue;

        if (isRecurring.HasValue)
            IsRecurring = isRecurring.Value;

        if (recurrenceJson != null)
            RecurrenceJson = recurrenceJson;

        if (audienceIds != null)
            AudienceIds = audienceIds;
    }

    private static void ValidateDateAndTime(DateOnly meetingDate, string startTime, string endTime)
    {
        // Validate time format HH:mm
        if (!IsValidTimeFormat(startTime))
            throw new ArgumentException($"Invalid start time format: {startTime}. Expected HH:mm");

        if (!IsValidTimeFormat(endTime))
            throw new ArgumentException($"Invalid end time format: {endTime}. Expected HH:mm");
    }

    private static bool IsValidTimeFormat(string time)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(time, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$");
    }
}
