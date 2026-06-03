using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Calendar.Events;

public class Event : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    public string Title { get; set; } = string.Empty;

    public Guid EventTypeId { get; set; }

    public string? Description { get; set; }

    public string? Poster { get; set; }

    public EventMode Mode { get; set; } = EventMode.Offline;

    public string? Venue { get; set; }

    public DateTimeOffset? MeetingDate { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public bool IsRecurring { get; set; }

    public string? RecurrenceJson { get; set; }

    public List<Guid> AudienceIds { get; set; } = new();

    public EventStatus GetStatus(DateTimeOffset now) =>
        EventScheduleRules.ResolveStatus(MeetingDate, now);

    protected Event() { }

    internal Event(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        Guid eventTypeId,
        EventDetails details) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        EventTypeId = eventTypeId;
        Title = details.Title;
        MeetingDate = details.MeetingDate;
        StartTime = details.StartTime;
        EndTime = details.EndTime;
        Description = details.Description;
        Poster = details.Poster;
        Mode = details.Mode ?? EventMode.Offline;
        Venue = details.Venue;
        IsRecurring = details.IsRecurring ?? false;
        RecurrenceJson = details.RecurrenceJson;
        AudienceIds = details.AudienceIds ?? new();
    }

    public static Event CreateMeetingEvent(
        Guid? tenantId,
        Guid academicYearId,
        string title,
        Guid eventTypeId,
        DateTimeOffset meetingDate,
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
        EventScheduleRules.ValidateTime(startTime, endTime);

        return EventFactory.Create(
            tenantId,
            academicYearId,
            eventTypeId,
            new EventDetails(title, meetingDate, startTime, endTime, description, poster, mode, venue, isRecurring, recurrenceJson, audienceIds));
    }

    public static Event CreateEvent(
        Guid? tenantId,
        Guid academicYearId,
        string title,
        Guid eventTypeId,
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
        EventScheduleRules.ValidateTime(startTime, endTime);

        return EventFactory.Create(
            tenantId,
            academicYearId,
            eventTypeId,
            new EventDetails(title, null, startTime, endTime, description, poster, mode, venue, isRecurring, recurrenceJson, audienceIds));
    }

    public void Update(
        string title,
        DateTimeOffset meetingDate,
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
        EventScheduleRules.ValidateTime(startTime, endTime);

        EventDetailsMutator.Apply(
            this,
            new EventDetails(title, meetingDate, startTime, endTime, description, poster, mode, venue, isRecurring, recurrenceJson, audienceIds));
    }

    public void UpdateMeetingEvent(
        string title,
        DateTimeOffset meetingDate,
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
        EventScheduleRules.ValidateTime(startTime, endTime);

        EventDetailsMutator.Apply(
            this,
            new EventDetails(title, meetingDate, startTime, endTime, description, poster, mode, venue, isRecurring, recurrenceJson, audienceIds));
    }

    public void UpdateEvent(
        string title,
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
        EventScheduleRules.ValidateTime(startTime, endTime);

        EventDetailsMutator.Apply(
            this,
            new EventDetails(title, null, startTime, endTime, description, poster, mode, venue, isRecurring, recurrenceJson, audienceIds));
    }

}
