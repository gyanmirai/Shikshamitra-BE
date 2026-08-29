using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class GetSubstitutionsInput : PagedAndSortedResultRequestDto
{
    public Guid? AcademicYearId { get; set; }
    public Guid? AcademicMediumId { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public DateTimeOffset? Date { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? OriginalTeacherId { get; set; }
    public Guid? SubstituteTeacherId { get; set; }
    public Guid? RoomId { get; set; }
    public SubstitutionStatus? Status { get; set; }
    public string? StatusText { get; set; }
    public string? Reason { get; set; }
    public SubstitutionPriority? Priority { get; set; }
    public SubstitutionAcknowledgementStatus? AcknowledgementStatus { get; set; }
    public SubstitutionSource? Source { get; set; }
    public SubstitutionConflictStatus? ConflictStatus { get; set; }
    public string? TeacherName { get; set; }
    public string? ClassName { get; set; }
    public string? DivisionName { get; set; }
    public string? SubjectName { get; set; }
}
public class SubstitutionDto : EntityDto<Guid>
{
    public DateTime Date { get; set; }
    public string Day { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public Guid? StandardId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid? DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public Guid? OriginalTeacherId { get; set; }
    public string OriginalTeacherName { get; set; } = string.Empty;
    public Guid? SubstituteTeacherId { get; set; }
    public string? SubstituteTeacherName { get; set; }
    public Guid? OriginalRoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid? AssignedRoomId { get; set; }
    public string? AssignedRoomName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string AcknowledgementStatus { get; set; } = string.Empty;
    public string NotificationStatus { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ConflictStatus { get; set; } = string.Empty;
    public string? ConflictReason { get; set; }
    public int? AiScore { get; set; }
    public bool IsAiSuggested { get; set; }
    public string WorkloadStatus { get; set; } = string.Empty;
    public bool AcknowledgementRequired { get; set; }
    public bool IsResponseWindowOpen { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AcknowledgedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? Remarks { get; set; }
    public List<SubstitutionAuditLogDto> AuditLog { get; set; } = new();
}

public class CreateSubstitutionDto
{
    public Guid? AcademicYearId { get; set; }
    public Guid? AcademicMediumId { get; set; }
    public DateTimeOffset Date { get; set; }
    public Guid? StandardId { get; set; }
    public string? ClassName { get; set; }
    public Guid? DivisionId { get; set; }
    public string? DivisionName { get; set; }
    public Guid? TimetableId { get; set; }
    public Guid? TimetableSlotId { get; set; }
    public Guid? TimetableSlotSubjectId { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public Guid? OriginalTeacherId { get; set; }
    public string? OriginalTeacherName { get; set; }
    public Guid? OriginalRoomId { get; set; }
    public string? RoomName { get; set; }
    public string Period { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Reason { get; set; } = "Teacher Absent";
    public SubstitutionPriority Priority { get; set; } = SubstitutionPriority.Normal;
    public SubstitutionSource Source { get; set; } = SubstitutionSource.Manual;
    public bool AcknowledgementRequired { get; set; } = true;
    public bool SendNotification { get; set; } = true;
    public Guid? SubstituteTeacherId { get; set; }
    public string? SubstituteTeacherName { get; set; }
    public Guid? AssignedRoomId { get; set; }
    public string? AssignedRoomName { get; set; }
    public string? Remarks { get; set; }
}

public class AssignSubstituteDto
{
    public Guid? SubstituteTeacherId { get; set; }
    public string SubstituteTeacherName { get; set; } = string.Empty;
    public Guid? AssignedRoomId { get; set; }
    public string? AssignedRoomName { get; set; }
    public string? Remarks { get; set; }
    public bool SendNotification { get; set; } = true;
}

public class SubstitutionActionDto
{
    public string? Reason { get; set; }
}

public class SubstitutionAuditLogDto
{
    public Guid Id { get; set; }
    public string At { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class SuggestedTeacherDto
{
    public Guid? TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public bool CanTeachSubject { get; set; }
    public bool HasConflict { get; set; }
    public string ConflictSeverity { get; set; } = "Clear";
    public string? ConflictReason { get; set; }
    public int TodaySubstitutionCount { get; set; }
    public int WeekSubstitutionCount { get; set; }
    public int MonthSubstitutionCount { get; set; }
    public int TodayTeachingLoad { get; set; }
    public int FreePeriods { get; set; }
    public string? LastDutyDate { get; set; }
    public string LoadStatus { get; set; } = "Low";
    public int Score { get; set; }
    public string CoverageStartTime { get; set; } = string.Empty;
    public string CoverageEndTime { get; set; } = string.Empty;
    public int CoverageMinutes { get; set; }
    public bool IsFullSlotCoverage { get; set; }
    public bool IsPartialCoverage { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
}

public class SuggestedTeacherInput
{
    public Guid? AcademicYearId { get; set; }
    public Guid? AcademicMediumId { get; set; }
    public DateTimeOffset Date { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? OriginalTeacherId { get; set; }
    public string? OriginalTeacherName { get; set; }
    public string? DepartmentName { get; set; }
    public string? SubjectName { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class SubstitutionCreateFormMetadataInput
{
    public Guid? AcademicYearId { get; set; }
    public Guid? AcademicMediumId { get; set; }
}

public class SubstitutionCreateFormMetadataDto
{
    public List<SubstitutionLookupOptionDto> Standards { get; set; } = new();
    public List<SubstitutionDivisionOptionDto> Divisions { get; set; } = new();
    public List<SubstitutionSubjectOptionDto> Subjects { get; set; } = new();
    public List<SubstitutionTeacherOptionDto> Teachers { get; set; } = new();
    public List<SubstitutionLookupOptionDto> Rooms { get; set; } = new();
}

public class SubstitutionLookupOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SubstitutionDivisionOptionDto : SubstitutionLookupOptionDto
{
    public Guid StandardId { get; set; }
}

public class SubstitutionSubjectOptionDto : SubstitutionLookupOptionDto
{
    public Guid StandardId { get; set; }
    public Guid AssignedSubjectId { get; set; }
    public Guid? PrimaryTeacherId { get; set; }
    public string? PrimaryTeacherName { get; set; }
}

public class SubstitutionTeacherOptionDto : SubstitutionLookupOptionDto
{
    public string DepartmentName { get; set; } = string.Empty;
}

public class SubstitutionLectureLookupInput
{
    public DateTimeOffset Date { get; set; }
    public Guid StandardId { get; set; }
    public Guid DivisionId { get; set; }
}

public class SubstitutionLectureLookupDto
{
    public Guid TimetableId { get; set; }
    public Guid TimetableSlotId { get; set; }
    public Guid SlotSubjectId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public Guid StandardId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid? OriginalTeacherId { get; set; }
    public string OriginalTeacherName { get; set; } = string.Empty;
    public Guid? OriginalRoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
}

public class SubstitutionDashboardDto
{
    public int Pending { get; set; }
    public int Assigned { get; set; }
    public int WaitingAck { get; set; }
    public int Acknowledged { get; set; }
    public int Completed { get; set; }
    public int Conflict { get; set; }
    public int TeacherUnavailable { get; set; }
    public int Uncovered { get; set; }
    public int ChangeRequested { get; set; }
    public SubstitutionDto? NextRisk { get; set; }
    public List<string> RiskAlerts { get; set; } = new();
    public List<SuggestedTeacherDto> WorkloadSnapshot { get; set; } = new();
}

public class UpdateSubstitutionDto : CreateSubstitutionDto
{
}

public class SubstitutionDailySheetInput : GetSubstitutionsInput
{
    public bool StudentVisible { get; set; }
}

public class SubstitutionDailySheetDto
{
    public DateTime Date { get; set; }
    public string GeneratedAt { get; set; } = string.Empty;
    public List<SubstitutionDto> Items { get; set; } = new();
}

public class SubstitutionAnalyticsInput
{
    public Guid? AcademicYearId { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? SubjectId { get; set; }
}

public class SubstitutionAnalyticsDto
{
    public int Total { get; set; }
    public int Covered { get; set; }
    public int Uncovered { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public int Conflicts { get; set; }
    public decimal CoverageRate { get; set; }
    public decimal AverageAcknowledgementMinutes { get; set; }
    public string TopSubstituteTeacher { get; set; } = string.Empty;
    public List<SubstitutionAnalyticsBreakdownDto> FrequentSubstitutes { get; set; } = new();
    public List<SubstitutionAnalyticsBreakdownDto> RepeatedOriginalTeacherAbsences { get; set; } = new();
    public List<SubstitutionAnalyticsBreakdownDto> SubjectCoverageLoss { get; set; } = new();
    public List<SubstitutionAnalyticsBreakdownDto> WorkloadPatterns { get; set; } = new();
}

public class SubstitutionAnalyticsBreakdownDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
