using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class CalendarImpactLog : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicCalendarDayId { get; private set; }
    
    public CalendarImpactType ImpactType { get; private set; }
    public Guid? AffectedEntityId { get; private set; }
    public string AffectedEntityName { get; private set; }
    public DateTime AffectedDate { get; private set; }
    
    public string Message { get; private set; }
    public CalendarImpactStatus Status { get; private set; }
    
    public DateTime? SuggestedNewDate { get; private set; }
    public bool IsResolved { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolutionNote { get; set; }

    private CalendarImpactLog()
    {
        /* ORM */
    }

    public CalendarImpactLog(
        Guid id,
        Guid? tenantId,
        Guid academicCalendarDayId,
        CalendarImpactType impactType,
        string affectedEntityName,
        DateTime affectedDate,
        string message,
        Guid? affectedEntityId = null,
        DateTime? suggestedNewDate = null)
        : base(id)
    {
        TenantId = tenantId;
        AcademicCalendarDayId = academicCalendarDayId;
        ImpactType = impactType;
        AffectedEntityName = affectedEntityName;
        AffectedDate = affectedDate.Date;
        Message = message;
        AffectedEntityId = affectedEntityId;
        SuggestedNewDate = suggestedNewDate?.Date;
        
        Status = CalendarImpactStatus.Pending;
        IsResolved = false;
    }

    public void Resolve(Guid userId, string? resolutionNote)
    {
        Status = CalendarImpactStatus.Resolved;
        IsResolved = true;
        ResolvedByUserId = userId;
        ResolvedAt = DateTime.UtcNow;
        ResolutionNote = resolutionNote;
    }

    public void Ignore(string reason)
    {
        Status = CalendarImpactStatus.IgnoredWithReason;
        IsResolved = true;
        ResolutionNote = reason;
    }

    public void Reschedule(DateTime newDate, string? note)
    {
        Status = CalendarImpactStatus.Rescheduled;
        IsResolved = true;
        SuggestedNewDate = newDate.Date;
        ResolutionNote = note;
    }
}
