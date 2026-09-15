using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportRequestDto
{
    [Required]
    public string ReportType { get; set; } = string.Empty;

    public string Format { get; set; } = AcademicReportFormat.Excel;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public AcademicReportFilterDto Filters { get; set; } = new();
}

public class AcademicReportFilterDto
{
    public Guid? AcademicYearId { get; set; }
    public List<Guid>? ClassIds { get; set; }
    public List<Guid>? DivisionIds { get; set; }
    public List<Guid>? SubjectIds { get; set; }
    public List<Guid>? TeacherIds { get; set; }
    public List<Guid>? RoomIds { get; set; }
    public Guid? ExamId { get; set; }
    public string? Grade { get; set; }
    public double? MinMarks { get; set; }
    public double? MaxMarks { get; set; }
    public List<Guid>? StudentIds { get; set; }
    public List<Guid>? AssignmentIds { get; set; }
    public List<Guid>? ChapterIds { get; set; }

    /// <summary>
    /// Upcoming = 1, In Progress = 2, Completed = 3
    /// </summary>
    public List<int>? Statuses { get; set; }

    public bool? RevisionRequired { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class AcademicReportPreviewDto
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public string FilterSummary { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AcademicReportColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
    public AcademicReportFilterDto NormalizedFilters { get; set; } = new();
}

public class AcademicReportColumnDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class AcademicReportExportCreateResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "Queued";
}

public class AcademicAiReportExportRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Prompt { get; set; } = string.Empty;

    [Required]
    public Guid? AcademicYearId { get; set; }

    [Required]
    public Guid? EducationBoardId { get; set; }

    [Required]
    public Guid? EducationMediumId { get; set; }

    [Required]
    public string Module { get; set; } = AcademicReportModules.Curriculum;

    public AcademicDynamicReportSpecDto? Plan { get; set; }

    [Required]
    public string ApprovalToken { get; set; } = string.Empty;
}

public class AcademicAiReportExportResponseDto : AcademicReportExportCreateResponseDto
{
    public string ReportType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public AcademicReportFilterDto Filters { get; set; } = new();
    public AcademicDynamicReportSpecDto Plan { get; set; } = new();
}

public class AcademicAiReportPlanRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Prompt { get; set; } = string.Empty;

    [Required]
    public Guid? AcademicYearId { get; set; }

    [Required]
    public Guid? EducationBoardId { get; set; }

    [Required]
    public Guid? EducationMediumId { get; set; }

    [Required]
    public string Module { get; set; } = AcademicReportModules.Curriculum;

    [MaxLength(6)]
    public List<AcademicAiReportClarificationAnswerDto> ClarificationAnswers { get; set; } = [];
}

public class AcademicAiReportPlanResponseDto
{
    public string Status { get; set; } = AcademicAiReportPlanStatuses.Ready;
    public AcademicDynamicReportSpecDto? Plan { get; set; }
    public List<AcademicAiReportClarificationQuestionDto> ClarificationQuestions { get; set; } = [];
    public List<string> Assumptions { get; set; } = [];
    public double Confidence { get; set; }
    public string PermissionSummary { get; set; } = string.Empty;
    public string? ApprovalToken { get; set; }
}

public class AcademicAiReportClarificationAnswerDto
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Answer { get; set; } = string.Empty;
}

public class AcademicAiReportClarificationQuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public string RecommendedOption { get; set; } = string.Empty;
}

public class AcademicAiReportPreviewRequestDto
{
    [Required]
    public Guid? AcademicYearId { get; set; }

    [Required]
    public Guid? EducationBoardId { get; set; }

    [Required]
    public Guid? EducationMediumId { get; set; }

    [Required]
    public string Module { get; set; } = AcademicReportModules.Curriculum;

    [Required]
    public AcademicDynamicReportSpecDto Plan { get; set; } = new();
}

public class AcademicAiReportPreviewResponseDto
{
    public AcademicDynamicReportSpecDto Plan { get; set; } = new();
    public AcademicReportPreviewDto Preview { get; set; } = new();
    public string PermissionSummary { get; set; } = string.Empty;
    public string ApprovalToken { get; set; } = string.Empty;
}

public class AcademicDynamicReportSpecDto
{
    [Required]
    public string Module { get; set; } = AcademicReportModules.Curriculum;

    [Required]
    public string Dataset { get; set; } = string.Empty;

    [Required]
    [StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(400)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(500)]
    public string InterpretedRequest { get; set; } = string.Empty;

    [MaxLength(4)]
    public List<string> RequestedFilterTypes { get; set; } = [];

    public List<AcademicDynamicReportAppliedFilterDto> AppliedFilters { get; set; } = [];

    [MinLength(1)]
    [MaxLength(20)]
    public List<string> Columns { get; set; } = [];

    public AcademicDynamicReportFilterDto Filters { get; set; } = new();

    public AcademicDynamicReportWhereDto Where { get; set; } = new();

    [MaxLength(3)]
    public List<AcademicDynamicReportSortDto> Sort { get; set; } = [];

    public string Format { get; set; } = AcademicReportFormat.Pdf;
}

public class AcademicDynamicReportAppliedFilterDto
{
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
}

public class AcademicDynamicReportFilterDto
{
    [MaxLength(250)]
    public List<string> ClassTokens { get; set; } = [];

    [MaxLength(250)]
    public List<string> DivisionTokens { get; set; } = [];

    [MaxLength(250)]
    public List<string> SubjectTokens { get; set; } = [];

    [MaxLength(250)]
    public List<string> ChapterTokens { get; set; } = [];
}

public class AcademicDynamicReportSortDto
{
    [Required]
    public string Field { get; set; } = string.Empty;

    [Required]
    public string Direction { get; set; } = AcademicDynamicSortDirections.Ascending;
}

public static class AcademicReportModules
{
    public const string Curriculum = "Curriculum";
    public const string HomeworkAssignment = "Homework & Assignment";
    public const string Homework = "Homework";
    public const string Assignment = "Assignment";
    public const string Examination = "Examination";
    public const string Timetable = "Timetable";
    public const string LearningManagement = "Learning Management";
    public const string StudentRecords = "Student Records";
    public const string StudentAttendance = "Student Attendance";
    public const string HumanResources = "HR";
    public const string Finance = "Finance";
    public const string Library = "Library";
    public const string Transport = "Transport";
}

public static class AcademicDynamicDatasets
{
    public const string CurriculumSubjects = "curriculum.subjects";
    public const string CurriculumChapters = "curriculum.chapters";
    public const string CurriculumTeachers = "curriculum.teachers";
    public const string TimetableSlots = "timetable.slots";
    public const string StudentRecordsStudents = "student-records.students";
    public const string StudentAttendanceEntries = "student-attendance.entries";
    public const string StudentAttendanceDailySummary = "student-attendance.daily-summary";
    public const string HomeworkTasks = "homework-assignment.homework";
    public const string AssignmentTasks = "homework-assignment.assignments";
    public const string ExaminationSchedule = "examination.schedule";
    public const string ExaminationMarks = "examination.marks";
    public const string ExaminationResults = "examination.results";
}

public static class AcademicDynamicSortDirections
{
    public const string Ascending = "ascending";
    public const string Descending = "descending";
}

public class AcademicDynamicReportWhereDto
{
    public string Match { get; set; } = AcademicDynamicMatchModes.All;

    [MaxLength(8)]
    public List<AcademicDynamicReportConditionDto> Conditions { get; set; } = [];
}

public class AcademicDynamicReportConditionDto
{
    [Required]
    public string Field { get; set; } = string.Empty;

    [Required]
    public string Operator { get; set; } = AcademicDynamicFilterOperators.Equal;

    [MaxLength(10)]
    public List<string> Values { get; set; } = [];
}

public static class AcademicAiReportPlanStatuses
{
    public const string NeedsClarification = "NeedsClarification";
    public const string Ready = "Ready";
}

public static class AcademicDynamicMatchModes
{
    public const string All = "all";
    public const string Any = "any";
}

public static class AcademicDynamicFilterOperators
{
    public const string Equal = "eq";
    public const string NotEquals = "ne";
    public const string Contains = "contains";
    public const string StartsWith = "startsWith";
    public const string In = "in";
    public const string GreaterThan = "gt";
    public const string GreaterThanOrEqual = "gte";
    public const string LessThan = "lt";
    public const string LessThanOrEqual = "lte";
    public const string Between = "between";
    public const string IsEmpty = "isEmpty";
    public const string IsNotEmpty = "isNotEmpty";

    public static readonly string[] All =
    [
        Equal, NotEquals, Contains, StartsWith, In, GreaterThan, GreaterThanOrEqual,
        LessThan, LessThanOrEqual, Between, IsEmpty, IsNotEmpty
    ];
}

public static class AcademicDynamicFilterTypes
{
    public const string Class = "class";
    public const string Division = "division";
    public const string Subject = "subject";
    public const string Chapter = "chapter";

    public static readonly string[] All = [Class, Division, Subject, Chapter];
}

public class AcademicReportExportJobDto
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? RequestJson { get; set; }
    public bool IsDynamic { get; set; }
    public string? Module { get; set; }
}

public class AcademicReportOptionsDto
{
    public List<AcademicReportOptionDto> ReportTypes { get; set; } = new();
    public List<AcademicReportOptionDto> Classes { get; set; } = new();
    public List<AcademicReportOptionDto> Subjects { get; set; } = new();
    public List<AcademicReportOptionDto> Statuses { get; set; } = new();
}

public class AcademicReportOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class AcademicReportDownloadDto
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public static class AcademicReportFormat
{
    public const string Pdf = "pdf";
    public const string Excel = "excel";
    public const string Csv = "csv";
}

public static class AcademicReportTypes
{
    public const string ChapterProgressReport = "Chapter Progress Report";
    public const string SubjectPrimaryTeacherReport = "Subject Primary Teacher Report";
    public const string SubjectWiseChapterCoverage = "Subject-wise Chapter Coverage";
    public const string ChapterDescriptionSummary = "Chapter Description Summary";
    public const string TimelineReport = "Timeline Report";
    public const string ClassWiseChapterAssignment = "Class-wise Chapter Assignment Report";
    public const string CurriculumCompletionReport = "Curriculum Completion Report";
    public const string ChapterRevisionTracker = "Chapter Revision Tracker";

    // Timetable reports
    public const string ClasswiseTimetable = "Class-wise Timetable Report";
    public const string TeacherwiseTimetable = "Teacher-wise Timetable Report";
    public const string RoomUtilizationReport = "Room Utilization Report";
    public const string SubjectwiseTimetableDistribution = "Subject-wise Timetable Distribution";
    public const string FreePeriodsReport = "Free Periods Report";
    public const string TimetableChangeLog = "Timetable Change Log Report";

    // Exam reports
    public const string GenderWiseGradeAnalysis = "Gender Wise Grade Analysis";
    public const string ResultStatisticsReport = "Result Statistics Report";
    public const string ResultList = "Result List";
    public const string GradewiseResultList = "Grade-wise Result List";
    public const string MarksRangeResultList = "Marks Range Result List";
    public const string SubjectwiseMarksReport = "Subject-wise Marks Report";
    public const string DetailedResultSheet = "Detailed Result Sheet";
    public const string ExamSummaryReport = "Exam Summary Report";

    // Student Ranking / Bright-Weak reports
    public const string BrightStudentAnalysis = "Bright Student Analysis";
    public const string BrightStudentListSubjectWise = "Bright Student List Subject Wise";
    public const string WeakStudentAnalysis = "Weak Student Analysis";
    public const string WeakStudentAnalysisSubjectWise = "Weak Student Analysis Subject Wise";
    public const string StudentGradeAnalysis = "Student Grade Analysis";
    public const string ResultListPassFailOverallAnalysis = "Result List Pass/Fail Overall Analysis";
    public const string ResultListOverallGradeAnalysis = "Result List Overall Grade Analysis";

    // Student-Wise Full Result reports
    public const string StudentAllBatchResult = "Student All Batch Result";
    public const string StudentAllExamResult = "Student All Exam Result";

    // Analytics & Monitoring reports
    public const string HomeworkLoadAnalysisReport = "Homework Load Analysis Report";
    public const string AssignmentLoadAnalysisReport = "Assignment Load Analysis Report";
    public const string TeacherPerformanceInHomeworkEvaluationReport = "Teacher Performance in Homework Evaluation Report";
    public const string StudentPerformanceTrendReport = "Student Performance Trend Report";
    public const string ClasswiseOverallHomeworkAssignmentReport = "Class-wise Overall Homework & Assignment Report";

    // Assignment reports
    public const string AssignmentIssuanceReport = "Assignment Issuance Report";
    public const string AssignmentSubmissionReport = "Assignment Submission Report";
    public const string AssignmentEvaluationReport = "Assignment Evaluation Report";
    public const string AssignmentMarksReport = "Assignment Marks Report";
    public const string LateSubmissionReport = "Late Submission Report";
    public const string MissingAssignmentReport = "Missing Assignment Report";
    public const string AssignmentFeedbackReport = "Assignment Feedback Report";
    public const string AssignmentGradeDistributionReport = "Assignment Grade Distribution Report";
    public const string AssignmentPerformanceReport = "Assignment Performance Report";

    // Homework reports
    public const string DailyHomeworkReport = "Daily Homework Report";
    public const string ClasswiseHomeworkReport = "Class-wise Homework Report";
    public const string SubjectwiseHomeworkReport = "Subject-wise Homework Report";
    public const string TeacherwiseHomeworkReport = "Teacher-wise Homework Report";
    public const string StudentwiseHomeworkSummaryReport = "Student-wise Homework Summary Report";
    public const string HomeworkCompletionStatusReport = "Homework Completion Status Report";
    public const string PendingHomeworkReport = "Pending Homework Report";
    public const string OverdueHomeworkReport = "Overdue Homework Report";
    public const string HomeworkSubmissionHistoryReport = "Homework Submission History Report";
    public const string HomeworkWithAttachmentsReport = "Homework with Attachments Report";
    public const string HomeworkAcknowledgementReport = "Homework Acknowledgement Report";

    // Subject Performance & Analysis reports
    public const string AverageSubjectMarkAnalysis = "Average Subject Mark Analysis";
    public const string SubjectAverageClassMarkAnalysis = "Subject Average Class Mark Analysis";
    public const string StudentGradeAnalysisSubjectWise = "Student Grade Analysis Subject Wise";
    public const string SubjectWiseGradeList = "Subject Wise Grade List";
    public const string ResultListPassFailAnalysisSubjectWise = "Result List Pass/Fail Analysis Subject Wise";
    public const string ResultListSubjectGradeAnalysis = "Result List Subject Grade Analysis";
    public const string ResultListWithSubject = "Result List With Subject";
    public const string ResultListWithSubjectFailedDetails = "Result List With Subject Failed Details";
    public const string ResultListWithSubjectGrade = "Result List With Subject Grade";
    public const string ResultListWithSubjectSmall = "Result List With Subject Small";
    public const string ResultListWithSubjectWithGrace = "Result List With Subject With Grace";
    public const string ResultWithAllSubjectList = "Result With All Subject List";
}
