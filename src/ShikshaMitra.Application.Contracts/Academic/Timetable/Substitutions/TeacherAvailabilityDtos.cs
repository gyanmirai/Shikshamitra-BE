using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class GetTeacherAvailabilityInput : PagedAndSortedResultRequestDto
{
    public Guid? AcademicYearId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? Date { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public TeacherAvailabilitySource? SourceType { get; set; }
}

public class TeacherAvailabilityDto : EntityDto<Guid>
{
    public Guid? TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime AvailabilityDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsFullDay { get; set; }
    public int AffectedLectureCount { get; set; }
    public bool NotifyLeadership { get; set; }
    public bool CanCreateSubstitution { get; set; }
    public List<string> ActiveSubstitutionStatuses { get; set; } = new();
    public List<Guid> ActiveSubstitutionIds { get; set; } = new();
    public List<Guid> PendingSubstitutionIds { get; set; } = new();
    public string? AttachmentName { get; set; }
    public string? Remarks { get; set; }
}

public class CreateTeacherAvailabilityDto
{
    public Guid? AcademicYearId { get; set; }
    public Guid? TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public DateTime AvailabilityDate { get; set; }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "14:15";
    public TeacherAvailabilityStatus AvailabilityStatus { get; set; } = TeacherAvailabilityStatus.Unavailable;
    public TeacherAvailabilitySource SourceType { get; set; } = TeacherAvailabilitySource.Manual;
    public Guid? SourceEntityId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsFullDay { get; set; }
    public bool NotifyLeadership { get; set; } = true;
    public string? AttachmentName { get; set; }
    public string? Remarks { get; set; }
}

public class AffectedLectureDto
{
    public Guid? TimetableId { get; set; }
    public Guid? TimetableSlotId { get; set; }
    public Guid? SlotSubjectId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public Guid? StandardId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid? DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid? RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid? TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public Guid? ExistingSubstitutionId { get; set; }
    public string? ExistingSubstitutionStatus { get; set; }
    public bool CanCreateSubstitution { get; set; } = true;
}

public class AffectedLectureResultDto
{
    public List<AffectedLectureDto> Items { get; set; } = new();
}
