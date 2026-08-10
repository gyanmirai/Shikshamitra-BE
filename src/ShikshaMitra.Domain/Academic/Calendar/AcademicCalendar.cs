using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendar : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? AcademicMediumId { get; private set; }
    public Guid? AcademicYearId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public CalendarStatus Status { get; private set; }
    public bool IsDefault { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public virtual ICollection<AcademicCalendarDay> Days { get; private set; }

    private AcademicCalendar()
    {
        /* ORM */
        Days = new List<AcademicCalendarDay>();
    }

    public AcademicCalendar(
        Guid id,
        Guid? tenantId,
        Guid academicMediumId,
        Guid? academicYearId,
        string name,
        DateTime startDate,
        DateTime endDate,
        string? description = null,
        bool isDefault = false)
        : base(id)
    {
        TenantId = tenantId;
        AcademicMediumId = academicMediumId;
        AcademicYearId = academicYearId;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
        IsDefault = isDefault;
        Status = CalendarStatus.Draft;
        Days = new List<AcademicCalendarDay>();
    }

    public void UpdateInfo(string name, string? description, bool isDefault)
    {
        Name = name;
        Description = description;
        IsDefault = isDefault;
    }

    public void Publish(Guid userId)
    {
        Status = CalendarStatus.Published;
        PublishedByUserId = userId;
        PublishedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = CalendarStatus.Archived;
    }
}
