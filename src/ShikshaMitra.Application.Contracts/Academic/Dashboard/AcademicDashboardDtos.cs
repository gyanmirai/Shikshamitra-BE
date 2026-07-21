using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Dashboard;

public class AdminAcademicDashboardDto
{
    public int CurriculumCompletion { get; set; }
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int AssignedSubjects { get; set; }
    public int ElectiveGroups { get; set; }
    public int PendingElectiveGroups { get; set; }
    public int TimetableRate { get; set; }
    public int ActiveTimetables { get; set; }
    public int PendingTimetables { get; set; }
    public int ConflictsCount { get; set; }
    public int ReadinessRate { get; set; }
    public int PendingGradeEntries { get; set; }
    public int PendingEvaluations { get; set; }
    public int UpcomingExams { get; set; }
    public List<AcademicDashboardInsightDto> Insights { get; set; } = [];
    public List<AcademicDashboardActivityDto> RecentActivities { get; set; } = [];
    public List<AcademicDashboardConfigItemDto> ConfigurationStatus { get; set; } = [];
    public List<AcademicDashboardOperationItemDto> OperationalStatus { get; set; } = [];
    public List<AcademicDashboardReadinessItemDto> ReadinessItems { get; set; } = [];
    public List<AcademicDashboardActionCenterItemDto> ActionCenterItems { get; set; } = [];
    public List<AcademicDashboardDataHealthItemDto> DataHealthItems { get; set; } = [];
    public AcademicDashboardPassFailChartDto SubjectPassFailChart { get; set; } = new();
    public AcademicDashboardPlannedVsActualChartDto PlannedVsActualProgressChart { get; set; } = new();
}

public class PrincipalAcademicDashboardDto
{
    public int SchoolPerformanceIndex { get; set; }
    public string SchoolAverageGrade { get; set; } = string.Empty;
    public string AverageAttendance { get; set; } = string.Empty;
    public int TotalTeachers { get; set; }
    public int TotalStudents { get; set; }
    public string PerformanceIndexHelper { get; set; } = string.Empty;
    public int SyllabusProgress { get; set; }
    public int MathProgress { get; set; }
    public int MathTarget { get; set; }
    public int ScienceProgress { get; set; }
    public int ScienceTarget { get; set; }
    public int EnglishProgress { get; set; }
    public int EnglishTarget { get; set; }
    public int ReadinessRate { get; set; }
    public string EvaluationLag { get; set; } = string.Empty;
    public int UpcomingExams { get; set; }
    public List<AcademicDashboardInsightDto> Insights { get; set; } = [];
    public List<AcademicDashboardReadinessItemDto> ReadinessItems { get; set; } = [];
    public List<AcademicDashboardActionCenterItemDto> ActionCenterItems { get; set; } = [];
    public List<AcademicDashboardDataHealthItemDto> DataHealthItems { get; set; } = [];
    public AcademicDashboardChapterCompletionChartDto ChapterCompletionChart { get; set; } = new();
    public AcademicDashboardPassFailChartDto SubjectPassFailChart { get; set; } = new();
    public List<AcademicDashboardTeacherPerformanceDto> TeacherPerformanceComparison { get; set; } = [];
    public List<AcademicDashboardExamDto> UpcomingExamsList { get; set; } = [];
    public List<AcademicDashboardPerformerDto> TopPerformers { get; set; } = [];
    public List<AcademicDashboardPerformerDto> BottomPerformers { get; set; } = [];
}

public class TeacherAcademicDashboardDto
{
    public int AttendanceRate { get; set; }
    public int TodaysLecturesCount { get; set; }
    public int LecturesDone { get; set; }
    public int LecturesOngoing { get; set; }
    public int PendingAttendanceClasses { get; set; }
    public int AssignedClassesCount { get; set; }
    public int AssignedSubjectsCount { get; set; }
    public int SystemAlertsCount { get; set; }
    public int EvaluationsRate { get; set; }
    public int PendingHomework { get; set; }
    public int PendingEvaluations { get; set; }
    public int UpcomingExams { get; set; }
    public int StudentsAttentionCount { get; set; }
    public List<AcademicDashboardInsightDto> Insights { get; set; } = [];
    public List<AcademicDashboardReadinessItemDto> ReadinessItems { get; set; } = [];
    public List<AcademicDashboardActionCenterItemDto> ActionCenterItems { get; set; } = [];
    public List<AcademicDashboardDataHealthItemDto> DataHealthItems { get; set; } = [];
    public List<AcademicDashboardLectureDto> TodaySchedule { get; set; } = [];
    public List<AcademicDashboardPendingTaskDto> PendingTasks { get; set; } = [];
    public List<AcademicDashboardAttentionStudentDto> StudentsAttention { get; set; } = [];
    public List<AcademicDashboardActivityDto> RecentActivities { get; set; } = [];
    public AcademicDashboardHomeworkStatusDto HomeworkStatus { get; set; } = new();
    public List<AcademicDashboardExamDto> UpcomingExamsList { get; set; } = [];
    public TeacherAcademicDashboardPerformanceDistributionDto PerformanceDistribution { get; set; } = new();
    public AcademicDashboardChapterCompletionChartDto ChapterProgressChart { get; set; } = new();
}

public class TeacherAcademicDashboardPerformanceDistributionDto
{
    public int Excellent { get; set; }
    public int Good { get; set; }
    public int Average { get; set; }
    public int NeedsImprovement { get; set; }
}

public class AcademicDashboardInsightDto
{
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string? ActionHref { get; set; }
}

public class AcademicDashboardActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class AcademicDashboardConfigItemDto
{
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class AcademicDashboardOperationItemDto
{
    public string Module { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class AcademicDashboardReadinessItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "missing";
    public string Severity { get; set; } = "warning";
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public int MissingCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string ActionHref { get; set; } = string.Empty;
}

public class AcademicDashboardActionCenterItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Source { get; set; } = string.Empty;
    public int Count { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string ActionHref { get; set; } = string.Empty;
}

public class AcademicDashboardDataHealthItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "healthy";
    public string Severity { get; set; } = "info";
    public string Detail { get; set; } = string.Empty;
    public string ActionHref { get; set; } = string.Empty;
}

public class AcademicDashboardPassFailChartDto
{
    public List<string> SubjectNames { get; set; } = [];
    public List<int> PassCounts { get; set; } = [];
    public List<int> FailCounts { get; set; } = [];
}

public class AcademicDashboardPlannedVsActualChartDto
{
    public List<string> Intervals { get; set; } = [];
    public List<int> PlannedPercentages { get; set; } = [];
    public List<int> ActualPercentages { get; set; } = [];
}

public class AcademicDashboardChapterCompletionChartDto
{
    public List<string> StandardNames { get; set; } = [];
    public List<int> CompletionPercentages { get; set; } = [];
}

public class AcademicDashboardTeacherPerformanceDto
{
    public string TeacherName { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
}

public class AcademicDashboardExamDto
{
    public string Id { get; set; } = string.Empty;
    public string ExamName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
}

public class AcademicDashboardPerformerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int TotalMarks { get; set; }
}

public class AcademicDashboardLectureDto
{
    public string Id { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string ClassDivision { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool AttendanceMarked { get; set; }
}

public class AcademicDashboardPendingTaskDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class AcademicDashboardAttentionStudentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ClassDivision { get; set; } = string.Empty;
    public int Attendance { get; set; }
    public int Performance { get; set; }
    public string Warning { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
}

public class AcademicDashboardHomeworkStatusDto
{
    public int Completed { get; set; }
    public int Pending { get; set; }
    public int Graded { get; set; }
}

public class ExamPassFailCountDto
{
    public int Passed { get; set; }
    public int Failed { get; set; }
}

public class ChapterCompletionFilteredDto
{
    public List<SubjectChapterCompletionDto> Subjects { get; set; } = [];
}

public class SubjectChapterCompletionDto
{
    public string SubjectName { get; set; } = string.Empty;
    public int CompletedChapters { get; set; }
    public int PendingChapters { get; set; }
}
