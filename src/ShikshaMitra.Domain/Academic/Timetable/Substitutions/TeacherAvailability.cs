using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class TeacherAvailability : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? AcademicYearId { get; private set; }
    public Guid? TeacherId { get; private set; }
    public string TeacherName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public DateTime AvailabilityDate { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public TeacherAvailabilityStatus AvailabilityStatus { get; private set; }
    public TeacherAvailabilitySource SourceType { get; private set; }
    public Guid? SourceEntityId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public bool IsFullDay { get; private set; }
    public int AffectedLectureCount { get; private set; }
    public bool NotifyLeadership { get; private set; }
    public string? AttachmentName { get; private set; }
    public string? Remarks { get; private set; }
    public bool IsCancelled { get; private set; }

    private TeacherAvailability()
    {
    }

    public TeacherAvailability(
        Guid id,
        Guid? tenantId,
        string teacherName,
        DateTime availabilityDate,
        TimeOnly startTime,
        TimeOnly endTime,
        TeacherAvailabilityStatus availabilityStatus,
        TeacherAvailabilitySource sourceType,
        string reason,
        bool isFullDay,
        bool notifyLeadership,
        Guid? academicYearId = null,
        Guid? teacherId = null,
        string? departmentName = null,
        Guid? sourceEntityId = null,
        int affectedLectureCount = 0,
        string? attachmentName = null,
        string? remarks = null)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        TeacherId = teacherId;
        TeacherName = teacherName;
        DepartmentName = departmentName ?? string.Empty;
        AvailabilityDate = DateTime.SpecifyKind(availabilityDate.Date, DateTimeKind.Utc);
        StartTime = startTime;
        EndTime = endTime;
        AvailabilityStatus = availabilityStatus;
        SourceType = sourceType;
        SourceEntityId = sourceEntityId;
        Reason = reason;
        IsFullDay = isFullDay;
        AffectedLectureCount = affectedLectureCount;
        NotifyLeadership = notifyLeadership;
        AttachmentName = attachmentName;
        Remarks = remarks;
    }

    public void SetAffectedLectureCount(int count)
    {
        AffectedLectureCount = count;
    }

    public void SetAcademicYear(Guid? academicYearId)
    {
        AcademicYearId = academicYearId;
    }

    public void Cancel()
    {
        IsCancelled = true;
    }
}
