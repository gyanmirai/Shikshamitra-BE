using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarWorkingHour : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicCalendarId { get; private set; }
    public Guid? StandardId { get; private set; }
    public Guid? DivisionId { get; private set; }
    
    public SchoolShiftType ShiftType { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    
    public TimeSpan? RecessStartTime { get; private set; }
    public TimeSpan? RecessEndTime { get; private set; }
    
    public bool IsDefault { get; private set; }

    private AcademicCalendarWorkingHour()
    {
        /* ORM */
    }

    public AcademicCalendarWorkingHour(
        Guid id,
        Guid? tenantId,
        Guid academicCalendarId,
        TimeSpan startTime,
        TimeSpan endTime,
        SchoolShiftType shiftType = SchoolShiftType.FullDay,
        Guid? standardId = null,
        Guid? divisionId = null,
        TimeSpan? recessStartTime = null,
        TimeSpan? recessEndTime = null,
        bool isDefault = true)
        : base(id)
    {
        TenantId = tenantId;
        AcademicCalendarId = academicCalendarId;
        StartTime = startTime;
        EndTime = endTime;
        ShiftType = shiftType;
        StandardId = standardId;
        DivisionId = divisionId;
        RecessStartTime = recessStartTime;
        RecessEndTime = recessEndTime;
        IsDefault = isDefault;
    }

    public void Update(
        TimeSpan startTime,
        TimeSpan endTime,
        SchoolShiftType shiftType,
        Guid? standardId,
        Guid? divisionId,
        TimeSpan? recessStartTime,
        TimeSpan? recessEndTime,
        bool isDefault)
    {
        StartTime = startTime;
        EndTime = endTime;
        ShiftType = shiftType;
        StandardId = standardId;
        DivisionId = divisionId;
        RecessStartTime = recessStartTime;
        RecessEndTime = recessEndTime;
        IsDefault = isDefault;
    }
}
