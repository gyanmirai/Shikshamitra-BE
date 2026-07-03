using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Calendar;

// ─── DTOs Mapped from Entities ──────────────────────────────────────────────

public class AcademicCalendarDto : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public CalendarStatus Status { get; set; }
    public bool IsDefault { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int TotalWorkingDays { get; set; }
    public int TotalHolidays { get; set; }
    public int TotalVacationDays { get; set; }
    public int TotalHalfDays { get; set; }
    public int TotalEvents { get; set; }
    public int TotalExamDays { get; set; }
    public int TotalPtmDays { get; set; }
    public int TotalEmergencyClosures { get; set; }
}

public class AcademicCalendarDayDto : EntityDto<Guid>
{
    public Guid AcademicCalendarId { get; set; }
    public DateTime Date { get; set; }
    public DayType DayType { get; set; }
    public HolidayCategory? HolidayCategory { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public bool AppliesToAll { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    public CalendarDayStatus Status { get; set; }
    public string? ChangeReason { get; set; }
}

public class EmergencyClosureListItemDto : EntityDto<Guid>
{
    public Guid AcademicCalendarId { get; set; }
    public string CalendarName { get; set; }
    public DateTime Date { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public bool AppliesToAll { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public CalendarDayStatus Status { get; set; }
    public string? ChangeReason { get; set; }
    public int ImpactCount { get; set; }
    public int PendingImpactCount { get; set; }
}

public class AcademicCalendarWorkingHourDto : EntityDto<Guid>
{
    public Guid AcademicCalendarId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public SchoolShiftType ShiftType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan? RecessStartTime { get; set; }
    public TimeSpan? RecessEndTime { get; set; }
    public bool IsDefault { get; set; }
}

public class AcademicCalendarSettingDto : EntityDto<Guid>
{
    public Guid AcademicYearId { get; set; }
    public WeekendRule WeekendRule { get; set; }
    public bool AllowClassSpecificHolidays { get; set; }
    public bool AllowDivisionSpecificHolidays { get; set; }
    public bool EnableHolidayCategories { get; set; }
    public bool EnableVacationRanges { get; set; }
    public bool EnableHalfDayRules { get; set; }
    public bool BlockTimetableOnHoliday { get; set; }
    public bool BlockTimetableOnVacation { get; set; }
    public bool BlockTimetableOnEmergencyClosure { get; set; }
    public bool BlockHomeworkDueDateOnHoliday { get; set; }
    public bool BlockHomeworkDueDateOnVacation { get; set; }
    public bool BlockExamScheduleOnHoliday { get; set; }
    public bool BlockExamScheduleOnVacation { get; set; }
    public bool AllowExamOnHalfDay { get; set; }
    public bool BlockLessonPlanOnHoliday { get; set; }
    public bool BlockLessonPlanOnVacation { get; set; }
    public bool BlockOnlineClassOnHoliday { get; set; }
    public bool BlockOnlineClassOnEventDay { get; set; }
    public bool AutoExemptAttendanceOnHoliday { get; set; }
    public bool AutoExemptAttendanceOnVacation { get; set; }
    public bool EnableEmergencyClosure { get; set; }
    public bool EmergencyClosureRequiresPrincipalApproval { get; set; }
    public bool AutoScanImpactedActivities { get; set; }
    public RescheduleMode RescheduleMode { get; set; }
    public bool NotifyTeachersOnCalendarChange { get; set; }
    public bool NotifyParentsOnCalendarChange { get; set; }
    public bool NotifyStudentsOnCalendarChange { get; set; }
    public bool NotifyStaffOnCalendarChange { get; set; }
    public CalendarVisibility CalendarVisibility { get; set; }
    public bool RequireReasonForPublishedCalendarChange { get; set; }
}

public class CalendarImpactLogDto : EntityDto<Guid>
{
    public Guid AcademicCalendarDayId { get; set; }
    public CalendarImpactType ImpactType { get; set; }
    public Guid? AffectedEntityId { get; set; }
    public string AffectedEntityName { get; set; }
    public DateTime AffectedDate { get; set; }
    public string Message { get; set; }
    public CalendarImpactStatus Status { get; set; }
    public DateTime? SuggestedNewDate { get; set; }
    public bool IsResolved { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}

public class AcademicCalendarAuditLogDto : EntityDto<Guid>
{
    public string Module { get; set; } = "CalendarManagement";
    public string? SubModule { get; set; }
    public string? EntityType { get; set; }
    public Guid AcademicCalendarId { get; set; }
    public Guid? AcademicCalendarDayId { get; set; }
    /// <summary>Generic entity reference for the FE: the day if this event is day-scoped, else the calendar.</summary>
    public Guid EntityId { get; set; }
    public string? EntityName { get; set; }

    public CalendarAuditAction Action { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string? Reason { get; set; }

    public CalendarAuditSource? Source { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? ChangedByRole { get; set; }
    public string? ChangedByEmail { get; set; }

    public DateTime Timestamp { get; set; }
    public string? CorrelationId { get; set; }
    public bool HasDiff { get; set; }
    public List<AuditLogDiffDto> Diff { get; set; } = new();
    public AuditLogMetadataDto? Metadata { get; set; }
}

public class AuditLogDiffDto
{
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangeType { get; set; } = "updated"; // added | updated | removed | unchanged
}

public class AuditLogMetadataDto
{
    public string? IpAddress { get; set; }
    public string? Device { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
}

public class AcademicCalendarAuditActiveUserDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public int ActionCount { get; set; }
}

public class AcademicCalendarAuditSummaryDto
{
    public int TotalAuditEvents { get; set; }
    public int ChangesThisWeek { get; set; }
    public int ChangesLastWeek { get; set; }
    public int CriticalChanges { get; set; }
    public AcademicCalendarAuditActiveUserDto? MostActiveUser { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

// ─── Input Payloads ─────────────────────────────────────────────────────────

public class CreateCalendarDto
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateCalendarDto
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateCalendarDayDto
{
    public DateTime Date { get; set; }
    public DayType DayType { get; set; }
    public HolidayCategory? HolidayCategory { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public bool AppliesToAll { get; set; }
    public Guid? StandardId { get; set; }
    // Preferred over StandardId when creating a marker for multiple classes;
    // one calendar day row is created per class.
    public List<Guid>? StandardIds { get; set; }
    public Guid? DivisionId { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? ChangeReason { get; set; }
}

public class CreateCalendarDaysBulkDto
{
    public List<CreateCalendarDayDto> Days { get; set; }
}

public class CreateEmergencyClosureDto
{
    public DateTime Date { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public bool AppliesToAll { get; set; }
    public List<Guid>? StandardIds { get; set; }
    public List<Guid>? DivisionIds { get; set; }
    public bool NotifyParents { get; set; }
    public bool NotifyTeachers { get; set; }
    public bool NotifyStudents { get; set; }
    public bool NotifyStaff { get; set; }
    public bool AutoScanImpacts { get; set; }
}

public class ResolveImpactLogDto
{
    public string? ResolutionNote { get; set; }
}

public class IgnoreImpactLogDto
{
    public string Reason { get; set; }
}

public class RescheduleImpactLogDto
{
    public DateTime NewDate { get; set; }
    public string? Note { get; set; }
}

public class CheckDateQueryDto
{
    public DateTime Date { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public string ValidationType { get; set; } // Timetable, HomeworkDueDate, ExamSchedule, etc.
}

public class CheckDateResponseDto
{
    public bool IsAllowed { get; set; }
    public DateTime Date { get; set; }
    public DayType? DayType { get; set; }
    public string? Title { get; set; }
    public string Message { get; set; }
    public string? BlockingReason { get; set; }
    public DateTime? SuggestedNextWorkingDate { get; set; }
}

public class CalendarDashboardDto
{
    public Guid AcademicYearId { get; set; }
    public int TotalWorkingDays { get; set; }
    public int TotalHolidays { get; set; }
    public int TotalVacationDays { get; set; }
    public int TotalHalfDays { get; set; }
    public int TotalEvents { get; set; }
    public int TotalExamDays { get; set; }
    public int TotalPtmDays { get; set; }
    public int TotalEmergencyClosures { get; set; }
    public int PendingImpactLogsCount { get; set; }
    public List<AcademicCalendarDayDto> UpcomingDays { get; set; } = new();
}
