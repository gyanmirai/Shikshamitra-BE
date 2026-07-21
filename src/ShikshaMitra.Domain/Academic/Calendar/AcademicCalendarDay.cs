using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarDay : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicCalendarId { get; private set; }
    public DateTime Date { get; private set; }
    public DayType DayType { get; private set; }
    public HolidayCategory? HolidayCategory { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public bool AppliesToAll { get; private set; }
    public Guid? StandardId { get; private set; }
    public Guid? DivisionId { get; private set; }

    public TimeSpan? StartTime { get; private set; }
    public TimeSpan? EndTime { get; private set; }
    public bool AutoScanImpacts { get; private set; }

    public CalendarDayStatus Status { get; private set; }
    public string? ChangeReason { get; private set; }

    private AcademicCalendarDay()
    {
        /* ORM */
    }

    public AcademicCalendarDay(
        Guid id,
        Guid? tenantId,
        Guid academicCalendarId,
        DateTime date,
        DayType dayType,
        string title,
        string? description = null,
        bool appliesToAll = true,
        Guid? standardId = null,
        Guid? divisionId = null,
        HolidayCategory? holidayCategory = null,
        TimeSpan? startTime = null,
        TimeSpan? endTime = null,
        bool autoScanImpacts = true)
        : base(id)
    {
        TenantId = tenantId;
        AcademicCalendarId = academicCalendarId;
        Date = date.Date; // Store only Date portion
        DayType = dayType;
        Title = title;
        Description = description;
        AppliesToAll = appliesToAll;
        StandardId = standardId;
        DivisionId = divisionId;
        HolidayCategory = holidayCategory;
        StartTime = startTime;
        EndTime = endTime;
        AutoScanImpacts = autoScanImpacts;

        Status = CalendarDayStatus.Active;
    }

    public void Update(
        DateTime date,
        DayType dayType,
        string title,
        string? description,
        bool appliesToAll,
        Guid? standardId,
        Guid? divisionId,
        HolidayCategory? holidayCategory,
        TimeSpan? startTime,
        TimeSpan? endTime,
        string? changeReason,
        bool? autoScanImpacts = null)
    {
        Date = date.Date;
        DayType = dayType;
        Title = title;
        Description = description;
        AppliesToAll = appliesToAll;
        StandardId = standardId;
        DivisionId = divisionId;
        HolidayCategory = holidayCategory;
        StartTime = startTime;
        EndTime = endTime;
        if (autoScanImpacts.HasValue)
        {
            AutoScanImpacts = autoScanImpacts.Value;
        }

        ChangeReason = changeReason;
    }

    public void Cancel(string? reason)
    {
        Status = CalendarDayStatus.Cancelled;
        ChangeReason = reason;
    }
}
