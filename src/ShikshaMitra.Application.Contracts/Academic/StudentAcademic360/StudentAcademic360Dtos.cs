using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;

namespace ShikshaMitra.Academic.StudentAcademic360;

public class StudentAcademic360Input
{
    public Guid? AcademicYearId { get; set; }
    public DateTime? AttendanceStartDate { get; set; }
    public DateTime? AttendanceEndDate { get; set; }
    public bool IncludeHistorical { get; set; }
    public int TaskSkipCount { get; set; }
    public int TaskMaxResultCount { get; set; } = 10;
    public int ExamSkipCount { get; set; }
    public int ExamMaxResultCount { get; set; } = 5;
    public int RecentTaskCount { get; set; } = 10;
    public int RecentExamCount { get; set; } = 5;
}

public class StudentAcademic360Dto
{
    public DateTime GeneratedAtUtc { get; set; }
    public StudentDto Student { get; set; } = null!;
    public StudentAcademic360HealthDto? AcademicHealth { get; set; }
    public List<StudentAcademic360KpiDto> Kpis { get; set; } = [];
    public StudentAcademic360AttendanceDto Attendance { get; set; } = new();
    public TimetableResponseDto Timetable { get; set; } = new();
    public StudentAcademic360LearningTaskSummaryDto LearningTasks { get; set; } = new();
    public List<StudentAcademic360ExamResultDto> Exams { get; set; } = [];
    public List<StudentAcademic360SubjectPerformanceDto> SubjectPerformance { get; set; } = [];
    public List<StudentAcademic360AlertDto> Alerts { get; set; } = [];
    public List<StudentAcademic360UpcomingItemDto> UpcomingItems { get; set; } = [];
    public List<StudentAcademic360TimelineEventDto> Timeline { get; set; } = [];
    public List<StudentAcademic360AchievementDto> Achievements { get; set; } = [];
    public List<StudentAcademic360InterventionDto> Interventions { get; set; } = [];
    public StudentAcademic360DataCompletenessDto DataCompleteness { get; set; } = new();
    public StudentAcademic360PermissionsDto Permissions { get; set; } = new();
    public List<StudentAcademic360SourceSnapshotDto> SourceSnapshot { get; set; } = [];
}

public class StudentAcademic360HealthDto
{
    public int? Score { get; set; }
    public string Status { get; set; } = "Insufficient Data";
    public string Trend { get; set; } = "Not enough data";
    public string Explanation { get; set; } = string.Empty;
    public List<StudentAcademic360HealthFactorDto> Factors { get; set; } = [];
}

public class StudentAcademic360HealthFactorDto
{
    public string Label { get; set; } = string.Empty;
    public int Earned { get; set; }
    public int Maximum { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public class StudentAcademic360KpiDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = "No data";
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = "no-data";
    public string Trend { get; set; } = "Not enough data";
    public string DataSource { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
}

public class StudentAcademic360AttendanceDto
{
    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int PendingDays { get; set; }
    public double Percentage { get; set; }
    public List<StudentAttendanceHistoryDto> RecentHistory { get; set; } = [];
}

public class StudentAcademic360LearningTaskSummaryDto
{
    public int TotalAssigned { get; set; }
    public int Submitted { get; set; }
    public int Pending { get; set; }
    public int Late { get; set; }
    public int Reviewed { get; set; }
    public int PendingEvaluation { get; set; }
    public List<StudentAcademic360LearningTaskDto> RecentTasks { get; set; } = [];
}

public class StudentAcademic360LearningTaskDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public decimal? Marks { get; set; }
    public decimal? TotalMarks { get; set; }
    public string? Feedback { get; set; }
}

public class StudentAcademic360ExamResultDto
{
    public Guid ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public int? Rank { get; set; }
    public double TotalMarks { get; set; }
    public double ObtainedMarks { get; set; }
    public double Percentage { get; set; }
    public string Grade { get; set; } = string.Empty;
    public List<StudentSubjectMarkDto> Subjects { get; set; } = [];
}

public class StudentAcademic360SubjectPerformanceDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int ExamCount { get; set; }
    public double AveragePercentage { get; set; }
    public string LatestGrade { get; set; } = string.Empty;
}

public class StudentAcademic360AlertDto
{
    public string Severity { get; set; } = "Information";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
}

public class StudentAcademic360UpcomingItemDto
{
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
}

public class StudentAcademic360TimelineEventDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Information";
    public string Source { get; set; } = string.Empty;
}

public class StudentAcademic360AchievementDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class StudentAcademic360InterventionDto
{
    public Guid? Id { get; set; }
    public string Concern { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? ReviewDate { get; set; }
    public string Status { get; set; } = "Not Started";
    public int Progress { get; set; }
    public string Outcome { get; set; } = string.Empty;
}

public class StudentAcademic360DataCompletenessDto
{
    public int Percentage { get; set; }
    public string Status { get; set; } = "Partial data";
    public List<string> MissingItems { get; set; } = [];
    public List<string> AvailableItems { get; set; } = [];
}

public class StudentAcademic360SourceSnapshotDto
{
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "No data";
    public int Count { get; set; }
    public DateTime? LastUpdatedAtUtc { get; set; }
}

public class StudentAcademic360PermissionsDto
{
    public bool CanViewRank { get; set; } = true;
    public bool CanViewParentContact { get; set; }
    public bool CanCreateIntervention { get; set; }
    public bool CanAddObservation { get; set; }
    public bool CanDownloadReport { get; set; }
}
