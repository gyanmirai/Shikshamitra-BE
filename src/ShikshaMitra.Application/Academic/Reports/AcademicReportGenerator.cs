using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator(
    IAsyncQueryableExecuter asyncExecuter,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<RoomType, Guid> roomTypeRepository,
    IEmployeeRepository employeeRepository,
    IRepository<ChapterProgress, Guid> chapterProgressRepository,
    IRepository<ExamMark, Guid> examMarkRepository,
    IRepository<Exam, Guid> examRepository,
    IStudentRepository studentRepository,
    StudentExamSubjectApplicabilityResolver subjectApplicabilityResolver,
    IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
    IRepository<StudentSubmissionEntity, Guid> submissionRepository,
    IRepository<AssignmentEntity, Guid> assignmentRepository,
    IRepository<GradingScale, Guid> gradingScaleRepository) : ITransientDependency
{
    private const int MaxPreviewPageSize = 500;
    private const int MaxFilterIds = 250;
    private static readonly HashSet<string> KnownReportTypes = typeof(AcademicReportTypes)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.FieldType == typeof(string))
        .Select(field => (string)field.GetValue(null)!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public AcademicReportRequestDto NormalizeAndValidateRequest(AcademicReportRequestDto request, bool exportAllRows)
    {
        if (request == null)
        {
            throw new BusinessException("Academic:Reports:RequestRequired");
        }

        request.ReportType = request.ReportType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.ReportType) || !KnownReportTypes.Contains(request.ReportType))
        {
            throw new BusinessException("Academic:Reports:UnknownReportType")
                .WithData("ReportType", request.ReportType);
        }

        request.Format = string.Equals(request.Format, AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
            ? AcademicReportFormat.Pdf
            : AcademicReportFormat.Excel;

        request.Page = request.Page <= 0 ? 1 : request.Page;
        if (request.PageSize <= 0)
        {
            request.PageSize = 50;
        }

        if (!exportAllRows && request.PageSize > MaxPreviewPageSize)
        {
            throw new BusinessException("Academic:Reports:PreviewPageSizeTooLarge")
                .WithData("MaxPageSize", MaxPreviewPageSize);
        }

        request.Filters ??= new AcademicReportFilterDto();
        ValidateDateRange(request.Filters);
        ValidateFilterIdCounts(request.Filters);
        ValidateMarksRange(request.Filters);

        return request;
    }

    public Task<AcademicReportOptionsDto> GetOptionsAsync(Guid userId, Guid? tenantId)
    {
        var dto = new AcademicReportOptionsDto
        {
            ReportTypes = new List<AcademicReportOptionDto>
            {
                new() { Id = AcademicReportTypes.ChapterProgressReport, Label = "Chapter Progress Report" },
                new() { Id = AcademicReportTypes.SubjectPrimaryTeacherReport, Label = "Subject Primary Teacher Report" },
                new() { Id = AcademicReportTypes.SubjectWiseChapterCoverage, Label = "Subject-wise Chapter Coverage" },
                new() { Id = AcademicReportTypes.ChapterDescriptionSummary, Label = "Chapter Description Summary" },
                new() { Id = AcademicReportTypes.TimelineReport, Label = "Timeline Report" },
                new() { Id = AcademicReportTypes.ClassWiseChapterAssignment, Label = "Class-wise Chapter Assignment Report" },
                new() { Id = AcademicReportTypes.CurriculumCompletionReport, Label = "Curriculum Completion Report" },
                new() { Id = AcademicReportTypes.ChapterRevisionTracker, Label = "Chapter Revision Tracker" },
                new() { Id = AcademicReportTypes.ClasswiseTimetable, Label = "Class-wise Timetable Report" },
                new() { Id = AcademicReportTypes.TeacherwiseTimetable, Label = "Teacher-wise Timetable Report" },
                new() { Id = AcademicReportTypes.RoomUtilizationReport, Label = "Room Utilization Report" },
                new() { Id = AcademicReportTypes.SubjectwiseTimetableDistribution, Label = "Subject-wise Timetable Distribution" },
                new() { Id = AcademicReportTypes.FreePeriodsReport, Label = "Free Periods Report" },
                new() { Id = AcademicReportTypes.TimetableChangeLog, Label = "Timetable Change Log Report" },
                new() { Id = AcademicReportTypes.GenderWiseGradeAnalysis, Label = "Gender Wise Grade Analysis" },
                new() { Id = AcademicReportTypes.ResultStatisticsReport, Label = "Result Statistics Report" },
                new() { Id = AcademicReportTypes.ResultList, Label = "Result List" },
                new() { Id = AcademicReportTypes.GradewiseResultList, Label = "Grade-wise Result List" },
                new() { Id = AcademicReportTypes.MarksRangeResultList, Label = "Marks Range Result List" },
                new() { Id = AcademicReportTypes.SubjectwiseMarksReport, Label = "Subject-wise Marks Report" },
                new() { Id = AcademicReportTypes.DetailedResultSheet, Label = "Detailed Result Sheet" },
                new() { Id = AcademicReportTypes.ExamSummaryReport, Label = "Exam Summary Report" },

                // Student Ranking / Bright-Weak
                new() { Id = AcademicReportTypes.BrightStudentAnalysis, Label = "Bright Student Analysis" },
                new() { Id = AcademicReportTypes.BrightStudentListSubjectWise, Label = "Bright Student List Subject Wise" },
                new() { Id = AcademicReportTypes.WeakStudentAnalysis, Label = "Weak Student Analysis" },
                new() { Id = AcademicReportTypes.WeakStudentAnalysisSubjectWise, Label = "Weak Student Analysis Subject Wise" },
                new() { Id = AcademicReportTypes.StudentGradeAnalysis, Label = "Student Grade Analysis" },
                new() { Id = AcademicReportTypes.ResultListPassFailOverallAnalysis, Label = "Result List Pass/Fail Overall Analysis" },
                new() { Id = AcademicReportTypes.ResultListOverallGradeAnalysis, Label = "Result List Overall Grade Analysis" },

                // Student-Wise Full Result
                new() { Id = AcademicReportTypes.StudentAllBatchResult, Label = "Student All Batch Result" },
                new() { Id = AcademicReportTypes.StudentAllExamResult, Label = "Student All Exam Result" },

                // Subject Performance & Analysis
                new() { Id = AcademicReportTypes.AverageSubjectMarkAnalysis, Label = "Average Subject Mark Analysis" },
                new() { Id = AcademicReportTypes.SubjectAverageClassMarkAnalysis, Label = "Subject Average Class Mark Analysis" },
                new() { Id = AcademicReportTypes.StudentGradeAnalysisSubjectWise, Label = "Student Grade Analysis Subject Wise" },
                new() { Id = AcademicReportTypes.SubjectWiseGradeList, Label = "Subject Wise Grade List" },
                new() { Id = AcademicReportTypes.ResultListPassFailAnalysisSubjectWise, Label = "Result List Pass/Fail Analysis Subject Wise" },
                new() { Id = AcademicReportTypes.ResultListSubjectGradeAnalysis, Label = "Result List Subject Grade Analysis" },
                new() { Id = AcademicReportTypes.ResultListWithSubject, Label = "Result List With Subject" },
                new() { Id = AcademicReportTypes.ResultListWithSubjectFailedDetails, Label = "Result List With Subject Failed Details" },
                new() { Id = AcademicReportTypes.ResultListWithSubjectGrade, Label = "Result List With Subject Grade" },
                new() { Id = AcademicReportTypes.ResultListWithSubjectSmall, Label = "Result List With Subject Small" },
                new() { Id = AcademicReportTypes.ResultListWithSubjectWithGrace, Label = "Result List With Subject With Grace" },

                // Homework reports
                new() { Id = AcademicReportTypes.DailyHomeworkReport, Label = "Daily Homework Report" },
                new() { Id = AcademicReportTypes.ClasswiseHomeworkReport, Label = "Class-wise Homework Report" },
                new() { Id = AcademicReportTypes.SubjectwiseHomeworkReport, Label = "Subject-wise Homework Report" },
                new() { Id = AcademicReportTypes.TeacherwiseHomeworkReport, Label = "Teacher-wise Homework Report" },
                new() { Id = AcademicReportTypes.StudentwiseHomeworkSummaryReport, Label = "Student-wise Homework Summary Report" },
                new() { Id = AcademicReportTypes.HomeworkCompletionStatusReport, Label = "Homework Completion Status Report" },
                new() { Id = AcademicReportTypes.PendingHomeworkReport, Label = "Pending Homework Report" },
                new() { Id = AcademicReportTypes.OverdueHomeworkReport, Label = "Overdue Homework Report" },
                new() { Id = AcademicReportTypes.HomeworkSubmissionHistoryReport, Label = "Homework Submission History Report" },
                new() { Id = AcademicReportTypes.HomeworkWithAttachmentsReport, Label = "Homework with Attachments Report" },
                new() { Id = AcademicReportTypes.HomeworkAcknowledgementReport, Label = "Homework Acknowledgement Report" },

                // Assignment reports
                new() { Id = AcademicReportTypes.AssignmentIssuanceReport, Label = "Assignment Issuance Report" },
                new() { Id = AcademicReportTypes.AssignmentSubmissionReport, Label = "Assignment Submission Report" },
                new() { Id = AcademicReportTypes.AssignmentEvaluationReport, Label = "Assignment Evaluation Report" },
                new() { Id = AcademicReportTypes.AssignmentMarksReport, Label = "Assignment Marks Report" },
                new() { Id = AcademicReportTypes.LateSubmissionReport, Label = "Late Submission Report" },
                new() { Id = AcademicReportTypes.MissingAssignmentReport, Label = "Missing Assignment Report" },
                new() { Id = AcademicReportTypes.AssignmentFeedbackReport, Label = "Assignment Feedback Report" },
                new() { Id = AcademicReportTypes.AssignmentGradeDistributionReport, Label = "Assignment Grade Distribution Report" },
                new() { Id = AcademicReportTypes.AssignmentPerformanceReport, Label = "Assignment Performance Report" },

                // Analytics & Monitoring reports
                new() { Id = AcademicReportTypes.HomeworkLoadAnalysisReport, Label = "Homework Load Analysis Report" },
                new() { Id = AcademicReportTypes.AssignmentLoadAnalysisReport, Label = "Assignment Load Analysis Report" },
                new() { Id = AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport, Label = "Teacher Performance in Homework Evaluation Report" },
                new() { Id = AcademicReportTypes.StudentPerformanceTrendReport, Label = "Student Performance Trend Report" },
                new() { Id = AcademicReportTypes.ClasswiseOverallHomeworkAssignmentReport, Label = "Class-wise Overall Homework & Assignment Report" }
            },
            Statuses = new List<AcademicReportOptionDto>
            {
                new() { Id = "1", Label = "Upcoming" },
                new() { Id = "2", Label = "In Progress" },
                new() { Id = "3", Label = "Completed" }
            }
        };

        return Task.FromResult(dto);
    }

    public async Task<AcademicReportPreviewDto> GenerateAsync(AcademicReportRequestDto request, Guid userId, Guid? tenantId, bool exportAllRows)
    {
        request = NormalizeAndValidateRequest(request, exportAllRows);
        var payload = new AcademicReportPreviewDto
        {
            ReportType = request.ReportType,
            GeneratedAtUtc = DateTime.UtcNow,
            FilterSummary = BuildFilterSummary(request.Filters),
            NormalizedFilters = request.Filters
        };

        var query = await assignedSubjectRepository.WithDetailsAsync(x => x.Subject, x => x.Chapters, x => x.Standard, x => x.Teachers);

        // Security scoping + filter application.
        if (request.Filters.SubjectIds?.Any() == true)
        {
            query = query.Where(x => request.Filters.SubjectIds.Contains(x.SubjectId));
        }

        if (request.Filters.ClassIds?.Any() == true)
        {
            query = query.Where(x => request.Filters.ClassIds.Contains(x.StandardId));
        }

        switch (request.ReportType)
        {
            case AcademicReportTypes.ChapterProgressReport:
                return await BuildChapterProgressReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.SubjectPrimaryTeacherReport:
                return await BuildSubjectPrimaryTeacherReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.SubjectWiseChapterCoverage:
                return await BuildSubjectWiseChapterCoverageAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ChapterDescriptionSummary:
                return await BuildChapterDescriptionSummaryAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.TimelineReport:
                return await BuildTimelineReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ClassWiseChapterAssignment:
                return await BuildClassWiseChapterAssignmentAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.CurriculumCompletionReport:
                return await BuildCurriculumCompletionReportAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ChapterRevisionTracker:
                return await BuildChapterRevisionTrackerAsync(query, request, payload, exportAllRows);
            case AcademicReportTypes.ClasswiseTimetable:
                return await BuildClasswiseTimetableAsync(request, payload, exportAllRows);
            case AcademicReportTypes.TeacherwiseTimetable:
                return await BuildTeacherwiseTimetableAsync(request, payload, exportAllRows);
            case AcademicReportTypes.RoomUtilizationReport:
                return await BuildRoomUtilizationAsync(request, payload, exportAllRows);
            case AcademicReportTypes.SubjectwiseTimetableDistribution:
                return await BuildSubjectwiseTimetableDistributionAsync(request, payload, exportAllRows);
            case AcademicReportTypes.FreePeriodsReport:
                return await BuildFreePeriodsAsync(request, payload, exportAllRows);
            case AcademicReportTypes.TimetableChangeLog:
                return await BuildTimetableChangeLogAsync(request, payload, exportAllRows);
            case AcademicReportTypes.GenderWiseGradeAnalysis:
                return await BuildGenderWiseGradeAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultStatisticsReport:
                return await BuildResultStatisticsReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultList:
                return await BuildResultListAsync(request, payload, exportAllRows);
            case AcademicReportTypes.GradewiseResultList:
                return await BuildGradewiseResultListAsync(request, payload, exportAllRows);
            case AcademicReportTypes.MarksRangeResultList:
                return await BuildMarksRangeResultListAsync(request, payload, exportAllRows);
            case AcademicReportTypes.SubjectwiseMarksReport:
                return await BuildSubjectwiseMarksReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.DetailedResultSheet:
                return await BuildDetailedResultSheetAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultWithAllSubjectList:
                return await BuildDetailedResultSheetAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ExamSummaryReport:
                return await BuildExamSummaryReportAsync(request, payload, exportAllRows);

            // Student Ranking / Bright-Weak
            case AcademicReportTypes.BrightStudentAnalysis:
                return await BuildBrightStudentAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.BrightStudentListSubjectWise:
                return await BuildBrightStudentListSubjectWiseAsync(request, payload, exportAllRows);
            case AcademicReportTypes.WeakStudentAnalysis:
                return await BuildWeakStudentAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.WeakStudentAnalysisSubjectWise:
                return await BuildWeakStudentAnalysisSubjectWiseAsync(request, payload, exportAllRows);
            case AcademicReportTypes.StudentGradeAnalysis:
                return await BuildStudentGradeAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListPassFailOverallAnalysis:
                return await BuildResultListPassFailOverallAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListOverallGradeAnalysis:
                return await BuildResultListOverallGradeAnalysisAsync(request, payload, exportAllRows);

            // Student-Wise Full Result
            case AcademicReportTypes.StudentAllBatchResult:
                return await BuildStudentAllBatchResultAsync(request, payload, exportAllRows);
            case AcademicReportTypes.StudentAllExamResult:
                return await BuildStudentAllExamResultAsync(request, payload, exportAllRows);

            // Subject Performance & Analysis
            case AcademicReportTypes.AverageSubjectMarkAnalysis:
                return await BuildAverageSubjectMarkAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.SubjectAverageClassMarkAnalysis:
                return await BuildSubjectAverageClassMarkAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.StudentGradeAnalysisSubjectWise:
                return await BuildStudentGradeAnalysisSubjectWiseAsync(request, payload, exportAllRows);
            case AcademicReportTypes.SubjectWiseGradeList:
                return await BuildSubjectWiseGradeListAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListPassFailAnalysisSubjectWise:
                return await BuildResultListPassFailAnalysisSubjectWiseAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListSubjectGradeAnalysis:
                return await BuildResultListSubjectGradeAnalysisAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListWithSubject:
                return await BuildResultListWithSubjectAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListWithSubjectFailedDetails:
                return await BuildResultListWithSubjectFailedDetailsAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListWithSubjectGrade:
                return await BuildResultListWithSubjectGradeAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListWithSubjectSmall:
                return await BuildResultListWithSubjectSmallAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ResultListWithSubjectWithGrace:
                return await BuildResultListWithSubjectWithGraceAsync(request, payload, exportAllRows);

            // Homework reports
            case AcademicReportTypes.DailyHomeworkReport:
                return await BuildDailyHomeworkReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ClasswiseHomeworkReport:
                return await BuildClasswiseHomeworkReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.SubjectwiseHomeworkReport:
                return await BuildSubjectwiseHomeworkReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.TeacherwiseHomeworkReport:
                return await BuildTeacherwiseHomeworkReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.StudentwiseHomeworkSummaryReport:
                return await BuildStudentwiseHomeworkSummaryReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.HomeworkCompletionStatusReport:
                return await BuildHomeworkCompletionStatusReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.PendingHomeworkReport:
                return await BuildPendingHomeworkReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.OverdueHomeworkReport:
                return await BuildOverdueHomeworkReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.HomeworkSubmissionHistoryReport:
                return await BuildHomeworkSubmissionHistoryReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.HomeworkWithAttachmentsReport:
                return await BuildHomeworkWithAttachmentsReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.HomeworkAcknowledgementReport:
                return await BuildHomeworkAcknowledgementReportAsync(request, payload, exportAllRows);

            // Assignment reports
            case AcademicReportTypes.AssignmentIssuanceReport:
                return await BuildAssignmentIssuanceReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentSubmissionReport:
                return await BuildAssignmentSubmissionReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentEvaluationReport:
                return await BuildAssignmentEvaluationReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentMarksReport:
                return await BuildAssignmentMarksReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.LateSubmissionReport:
                return await BuildLateSubmissionReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.MissingAssignmentReport:
                return await BuildMissingAssignmentReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentFeedbackReport:
                return await BuildAssignmentFeedbackReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentGradeDistributionReport:
                return await BuildAssignmentGradeDistributionReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentPerformanceReport:
                return await BuildAssignmentPerformanceReportAsync(request, payload, exportAllRows);

            // Analytics & Monitoring reports
            case AcademicReportTypes.HomeworkLoadAnalysisReport:
                return await BuildHomeworkLoadAnalysisReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.AssignmentLoadAnalysisReport:
                return await BuildAssignmentLoadAnalysisReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport:
                return await BuildTeacherPerformanceInHomeworkEvaluationReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.StudentPerformanceTrendReport:
                return await BuildStudentPerformanceTrendReportAsync(request, payload, exportAllRows);
            case AcademicReportTypes.ClasswiseOverallHomeworkAssignmentReport:
                return await BuildClasswiseOverallHomeworkAssignmentReportAsync(request, payload, exportAllRows);

            default:
                throw new BusinessException("Academic:Reports:UnknownReportType")
                    .WithData("ReportType", request.ReportType);
        }
    }

    private static void ValidateDateRange(AcademicReportFilterDto filters)
    {
        if (filters.FromDate.HasValue && filters.ToDate.HasValue && filters.FromDate.Value.Date > filters.ToDate.Value.Date)
        {
            throw new BusinessException("Academic:Reports:InvalidDateRange")
                .WithData("FromDate", filters.FromDate.Value.ToString("yyyy-MM-dd"))
                .WithData("ToDate", filters.ToDate.Value.ToString("yyyy-MM-dd"));
        }

        if (filters.FromDate.HasValue && filters.ToDate.HasValue &&
            (filters.ToDate.Value.Date - filters.FromDate.Value.Date).TotalDays > 370)
        {
            throw new BusinessException("Academic:Reports:DateRangeTooLarge")
                .WithData("MaxDays", 370);
        }
    }

    private static void ValidateFilterIdCounts(AcademicReportFilterDto filters)
    {
        EnsureFilterIdCount(filters.ClassIds, nameof(filters.ClassIds));
        EnsureFilterIdCount(filters.DivisionIds, nameof(filters.DivisionIds));
        EnsureFilterIdCount(filters.SubjectIds, nameof(filters.SubjectIds));
        EnsureFilterIdCount(filters.TeacherIds, nameof(filters.TeacherIds));
        EnsureFilterIdCount(filters.RoomIds, nameof(filters.RoomIds));
        EnsureFilterIdCount(filters.StudentIds, nameof(filters.StudentIds));
        EnsureFilterIdCount(filters.AssignmentIds, nameof(filters.AssignmentIds));
        EnsureFilterIdCount(filters.ChapterIds, nameof(filters.ChapterIds));
    }

    private static void EnsureFilterIdCount(List<Guid>? ids, string filterName)
    {
        if (ids is { Count: > MaxFilterIds })
        {
            throw new BusinessException("Academic:Reports:TooManyFilterValues")
                .WithData("Filter", filterName)
                .WithData("MaxCount", MaxFilterIds);
        }
    }

    private static void ValidateMarksRange(AcademicReportFilterDto filters)
    {
        if (filters.MinMarks.HasValue && filters.MinMarks.Value < 0)
        {
            throw new BusinessException("Academic:Reports:InvalidMarksRange")
                .WithData("MinMarks", filters.MinMarks);
        }

        if (filters.MaxMarks.HasValue && filters.MaxMarks.Value < 0)
        {
            throw new BusinessException("Academic:Reports:InvalidMarksRange")
                .WithData("MaxMarks", filters.MaxMarks);
        }

        if (filters.MinMarks.HasValue && filters.MaxMarks.HasValue && filters.MinMarks.Value > filters.MaxMarks.Value)
        {
            throw new BusinessException("Academic:Reports:InvalidMarksRange")
                .WithData("MinMarks", filters.MinMarks)
                .WithData("MaxMarks", filters.MaxMarks);
        }
    }

    private static string BuildFilterSummary(AcademicReportFilterDto filters)
    {
        var parts = new List<string>();
        if (filters.AcademicYearId.HasValue) parts.Add($"AcademicYear={filters.AcademicYearId}");
        if (filters.ClassIds is { Count: > 0 }) parts.Add($"Classes={filters.ClassIds.Count}");
        if (filters.DivisionIds is { Count: > 0 }) parts.Add($"Divisions={filters.DivisionIds.Count}");
        if (filters.SubjectIds is { Count: > 0 }) parts.Add($"Subjects={filters.SubjectIds.Count}");
        if (filters.TeacherIds is { Count: > 0 }) parts.Add($"Teachers={filters.TeacherIds.Count}");
        if (filters.StudentIds is { Count: > 0 }) parts.Add($"Students={filters.StudentIds.Count}");
        if (filters.ExamId.HasValue) parts.Add($"Exam={filters.ExamId}");
        if (filters.FromDate.HasValue || filters.ToDate.HasValue)
        {
            parts.Add($"Date={filters.FromDate?.ToString("yyyy-MM-dd") ?? "*"}..{filters.ToDate?.ToString("yyyy-MM-dd") ?? "*"}");
        }

        return parts.Count == 0 ? "No filters" : string.Join("; ", parts);
    }

    private string GetChapterStatus(DateTimeOffset start, DateTimeOffset end)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < start) return "Upcoming";
        if (now >= start && now <= end) return "In Progress";
        return "Completed";
    }

    private async Task<AcademicReportPreviewDto> BuildChapterProgressReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject Name" },
            new() { Id = "ChapterName", Label = "Chapter Name" },
            new() { Id = "Progress", Label = "Progress (%)" },
            new() { Id = "TeacherName", Label = "Teacher Name" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        if (!subjects.Any())
        {
            payload.TotalCount = 0;
            return payload;
        }

        var (_, _, _, _, _, teachers) = await LoadLookupsAsync(includeTeachers: true);
        var chapterIds = subjects.SelectMany(x => x.Chapters.Select(c => c.Id)).ToList();
        var progressMap = await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var s in subjects.OrderBy(x => x.Subject?.Name))
        {
            var teacher = s.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary)
                           ?? s.Teachers.FirstOrDefault();
            var teacherName = teacher != null ? teachers.GetValueOrDefault(teacher.TeacherId, "—") : "—";

            foreach (var chapter in s.Chapters.OrderBy(c => c.ChapterNumber))
            {
                if (request.Filters.ChapterIds?.Any() == true && !request.Filters.ChapterIds.Contains(chapter.Id))
                {
                    continue;
                }

                var progress = progressMap.TryGetValue(chapter.Id, out var p) ? p.Progress : 0;
                rows.Add(new Dictionary<string, string?>
                {
                    { "SubjectName", s.Subject?.Name ?? "—" },
                    { "ChapterName", chapter.ChapterName },
                    { "Progress", $"{progress}%" },
                    { "TeacherName", teacherName }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectPrimaryTeacherReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject Name" },
            new() { Id = "PrimaryTeacher", Label = "Primary Teacher" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var (_, _, _, _, _, teachers) = await LoadLookupsAsync(includeTeachers: true);
        var rows = subjects
            .OrderBy(subject => subject.Subject?.Name)
            .ThenBy(subject => subject.Standard?.Name)
            .Select(subject =>
            {
                var primaryTeacher = subject.Teachers.FirstOrDefault(
                    teacher => teacher.TeacherType == SubjectTeacherType.Primary);
                return new Dictionary<string, string?>
                {
                    { "SubjectName", subject.Subject?.Name ?? "—" },
                    {
                        "PrimaryTeacher",
                        primaryTeacher == null
                            ? "—"
                            : teachers.GetValueOrDefault(primaryTeacher.TeacherId, "—")
                    }
                };
            })
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectWiseChapterCoverageAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject" },
            new() { Id = "TotalChapters", Label = "Total Chapters" },
            new() { Id = "CoveredChapters", Label = "Covered Chapters" },
            new() { Id = "RemainingChapters", Label = "Remaining Chapters" },
            new() { Id = "CoveragePercent", Label = "Coverage %" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var chapterIds = subjects.SelectMany(x => x.Chapters.Select(c => c.Id)).ToList();
        var progressMap = await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);

        var rows = subjects.OrderBy(x => x.Subject?.Name).Select(s =>
        {
            var total = s.Chapters.Count;
            var covered = s.Chapters.Count(c => progressMap.TryGetValue(c.Id, out var p) && p.Progress == 100);
            var remaining = total - covered;
            var coveragePct = total == 0 ? 0 : Math.Round((double)covered / total * 100, 2);
            return new Dictionary<string, string?>
            {
                { "SubjectName", s.Subject?.Name ?? "—" },
                { "TotalChapters", total.ToString() },
                { "CoveredChapters", covered.ToString() },
                { "RemainingChapters", remaining.ToString() },
                { "CoveragePercent", $"{coveragePct}%" }
            };
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildChapterDescriptionSummaryAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject" },
            new() { Id = "ChapterNumber", Label = "Chapter Number" },
            new() { Id = "ChapterName", Label = "Chapter Name" },
            new() { Id = "Description", Label = "Chapter Description" },
            new() { Id = "LearningObjectives", Label = "Learning Objectives" },
            new() { Id = "TotalTopics", Label = "Total Topics" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var rows = new List<Dictionary<string, string?>>();
        foreach (var s in subjects.OrderBy(x => x.Subject?.Name))
        {
            foreach (var chapter in s.Chapters.OrderBy(c => c.ChapterNumber))
            {
                if (request.Filters.ChapterIds?.Any() == true && !request.Filters.ChapterIds.Contains(chapter.Id))
                {
                    continue;
                }

                rows.Add(new Dictionary<string, string?>
                {
                    { "SubjectName", s.Subject?.Name ?? "—" },
                    { "ChapterNumber", chapter.ChapterNumber.ToString() },
                    { "ChapterName", chapter.ChapterName },
                    { "Description", System.Text.RegularExpressions.Regex.Replace(chapter.Description ?? string.Empty, "<.*?>", string.Empty) },
                    { "LearningObjectives", string.Empty },
                    { "TotalTopics", string.Empty }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTimelineReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject" },
            new() { Id = "ChapterName", Label = "Chapter" },
            new() { Id = "PlannedStartDate", Label = "Planned Start Date" },
            new() { Id = "PlannedEndDate", Label = "Planned End Date" },
            new() { Id = "ActualStartDate", Label = "Actual Start Date" },
            new() { Id = "ActualEndDate", Label = "Actual End Date" },
            new() { Id = "DelayDays", Label = "Delay Days" },
            new() { Id = "Status", Label = "Status" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var chapterIds = subjects.SelectMany(x => x.Chapters.Select(c => c.Id)).ToList();
        var progressMap = await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var s in subjects.OrderBy(x => x.Subject?.Name))
        {
            foreach (var chapter in s.Chapters.OrderBy(c => c.ChapterStartTime))
            {
                progressMap.TryGetValue(chapter.Id, out var prog);
                var actualStart = prog.StartDate;
                var actualEnd = prog.CompletionDate;

                var delayDays = string.Empty;
                if (actualEnd.HasValue && actualEnd.Value > chapter.ChapterEndTime.DateTime)
                    delayDays = ((int)(actualEnd.Value - chapter.ChapterEndTime.DateTime).TotalDays).ToString();

                var status = prog.Progress == 100 ? "Completed"
                    : prog.Progress > 0 ? "In Progress"
                    : "Upcoming";

                rows.Add(new Dictionary<string, string?>
                {
                    { "SubjectName", s.Subject?.Name ?? "—" },
                    { "ChapterName", chapter.ChapterName },
                    { "PlannedStartDate", chapter.ChapterStartTime.ToString("dd-MM-yyyy") },
                    { "PlannedEndDate", chapter.ChapterEndTime.ToString("dd-MM-yyyy") },
                    { "ActualStartDate", actualStart.HasValue ? actualStart.Value.ToString("dd-MM-yyyy") : string.Empty },
                    { "ActualEndDate", actualEnd.HasValue ? actualEnd.Value.ToString("dd-MM-yyyy") : string.Empty },
                    { "DelayDays", delayDays },
                    { "Status", status }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildClassWiseChapterAssignmentAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class" },
            new() { Id = "SubjectName", Label = "Subject" },
            new() { Id = "ChapterName", Label = "Chapter" },
            new() { Id = "AssignedTeacher", Label = "Assigned Teacher" },
            new() { Id = "AssignmentDate", Label = "Assignment Date" },
            new() { Id = "Status", Label = "Status" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var (_, _, _, _, _, teachers) = await LoadLookupsAsync(includeTeachers: true);
        var chapterIds = subjects.SelectMany(x => x.Chapters.Select(c => c.Id)).ToList();
        var progressMap = await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var s in subjects.OrderBy(x => x.Standard?.Name).ThenBy(x => x.Subject?.Name))
        {
            var teacher = s.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary)
                           ?? s.Teachers.FirstOrDefault();
            var teacherName = teacher != null ? teachers.GetValueOrDefault(teacher.TeacherId, "—") : "—";

            foreach (var chapter in s.Chapters.OrderBy(c => c.ChapterNumber))
            {
                progressMap.TryGetValue(chapter.Id, out var prog);
                var status = prog.Progress == 100 ? "Completed"
                    : prog.Progress > 0 ? "In Progress"
                    : "Upcoming";

                rows.Add(new Dictionary<string, string?>
                {
                    { "ClassName", s.Standard?.Name ?? "—" },
                    { "SubjectName", s.Subject?.Name ?? "—" },
                    { "ChapterName", chapter.ChapterName },
                    { "AssignedTeacher", teacherName },
                    { "AssignmentDate", chapter.CreationTime.ToString("dd-MM-yyyy") },
                    { "Status", status }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildCurriculumCompletionReportAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class" },
            new() { Id = "SubjectName", Label = "Subject" },
            new() { Id = "TotalChapters", Label = "Total Chapters" },
            new() { Id = "CompletedChapters", Label = "Completed Chapters" },
            new() { Id = "CompletionPercent", Label = "Completion %" },
            new() { Id = "RemainingChapters", Label = "Remaining Chapters" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var chapterIds = subjects.SelectMany(x => x.Chapters.Select(c => c.Id)).ToList();
        var progressMap = await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);

        var rows = subjects.OrderBy(x => x.Standard?.Name).ThenBy(x => x.Subject?.Name).Select(s =>
        {
            var total = s.Chapters.Count;
            var completed = s.Chapters.Count(c => progressMap.TryGetValue(c.Id, out var p) && p.Progress == 100);
            var remaining = total - completed;
            var completionPct = total == 0 ? 0 : Math.Round((double)completed / total * 100, 2);
            return new Dictionary<string, string?>
            {
                { "ClassName", s.Standard?.Name ?? "—" },
                { "SubjectName", s.Subject?.Name ?? "—" },
                { "TotalChapters", total.ToString() },
                { "CompletedChapters", completed.ToString() },
                { "CompletionPercent", $"{completionPct}%" },
                { "RemainingChapters", remaining.ToString() }
            };
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildChapterRevisionTrackerAsync(
        IQueryable<AssignedSubject> query,
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubjectName", Label = "Subject" },
            new() { Id = "ChapterName", Label = "Chapter" },
            new() { Id = "FirstCompletionDate", Label = "First Completion Date" },
            new() { Id = "RevisionCount", Label = "Revision Count" },
            new() { Id = "TeacherName", Label = "Teacher" }
        };

        var subjects = await asyncExecuter.ToListAsync(query);
        var (_, _, _, _, _, teachers) = await LoadLookupsAsync(includeTeachers: true);
        var chapterIds = subjects.SelectMany(x => x.Chapters.Select(c => c.Id)).ToList();
        var progressMap = await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var s in subjects.OrderBy(x => x.Subject?.Name))
        {
            var teacher = s.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary)
                           ?? s.Teachers.FirstOrDefault();
            var teacherName = teacher != null ? teachers.GetValueOrDefault(teacher.TeacherId, "—") : "—";

            foreach (var chapter in s.Chapters.OrderBy(c => c.ChapterNumber))
            {
                if (request.Filters.ChapterIds?.Any() == true && !request.Filters.ChapterIds.Contains(chapter.Id))
                {
                    continue;
                }

                progressMap.TryGetValue(chapter.Id, out var prog);
                var completionDate = prog.CompletionDate.HasValue
                    ? prog.CompletionDate.Value.ToString("dd-MM-yyyy")
                    : string.Empty;

                rows.Add(new Dictionary<string, string?>
                {
                    { "SubjectName", s.Subject?.Name ?? "—" },
                    { "ChapterName", chapter.ChapterName },
                    { "FirstCompletionDate", completionDate },
                    { "RevisionCount", string.Empty },
                    { "TeacherName", teacherName }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Timetable Reports ────────────────────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildClasswiseTimetableAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "StartTime", Label = "Start Time" },
            new() { Id = "EndTime", Label = "End Time" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "Room", Label = "Room" },
            new() { Id = "Status", Label = "Status" }
        };

        var (standards, divisions, subjects, rooms, _, teachers) = await LoadLookupsAsync(
            includeStandards: true, includeDivisions: true, includeSubjects: true, includeRooms: true, includeTeachers: true);

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync();
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList.OrderBy(t => standards.GetValueOrDefault(t.StandardId, "")).ThenBy(t => divisions.GetValueOrDefault(t.DivisionId, "")))
        {
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period).OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                var startTime = slot.StartTime.ToString("HH:mm");
                var endTime = slot.EndTime.ToString("HH:mm");
                var periodLabel = $"Period {slot.Sequence}";
                if (!slot.Subjects.Any())
                {
                    rows.Add(new()
                    {
                        { "Day", slot.Day.ToString() }, { "Period", periodLabel },
                        { "StartTime", startTime }, { "EndTime", endTime },
                        { "Subject", "—" }, { "Teacher", "—" }, { "Room", "—" }, { "Status", "Free" }
                    });
                    continue;
                }
                foreach (var s in slot.Subjects)
                {
                    rows.Add(new()
                    {
                        { "Day", slot.Day.ToString() }, { "Period", periodLabel },
                        { "StartTime", startTime }, { "EndTime", endTime },
                        { "Subject", subjects.GetValueOrDefault(s.SubjectId, "—") },
                        { "Teacher", s.TeacherId.HasValue ? teachers.GetValueOrDefault(s.TeacherId.Value, "—") : "—" },
                        { "Room", s.RoomId.HasValue ? rooms.GetValueOrDefault(s.RoomId.Value, "—") : "—" },
                        { "Status", "Scheduled" }
                    });
                }
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTeacherwiseTimetableAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "TeacherName", Label = "Teacher Name" },
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Room", Label = "Room" },
            new() { Id = "StartTime", Label = "Start Time" },
            new() { Id = "EndTime", Label = "End Time" }
        };

        var (standards, divisions, subjects, rooms, _, teachers) = await LoadLookupsAsync(
            includeStandards: true, includeDivisions: true, includeSubjects: true, includeRooms: true, includeTeachers: true);

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync();
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList)
        {
            var className = standards.GetValueOrDefault(tt.StandardId, "—");
            var divisionName = divisions.GetValueOrDefault(tt.DivisionId, "—");
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period).OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                foreach (var s in slot.Subjects)
                {
                    if (!s.TeacherId.HasValue) continue;
                    if (request.Filters.TeacherIds?.Any() == true && !request.Filters.TeacherIds.Contains(s.TeacherId.Value))
                        continue;
                    rows.Add(new()
                    {
                        { "TeacherName", teachers.GetValueOrDefault(s.TeacherId.Value, "—") },
                        { "Day", slot.Day.ToString() },
                        { "Period", $"Period {slot.Sequence}" },
                        { "Subject", subjects.GetValueOrDefault(s.SubjectId, "—") },
                        { "Class", className },
                        { "Division", divisionName },
                        { "Room", s.RoomId.HasValue ? rooms.GetValueOrDefault(s.RoomId.Value, "—") : "—" },
                        { "StartTime", slot.StartTime.ToString("HH:mm") },
                        { "EndTime", slot.EndTime.ToString("HH:mm") }
                    });
                }
            }
        }

        rows = rows.OrderBy(r => r["TeacherName"]).ThenBy(r => r["Day"]).ThenBy(r => r["Period"]).ToList();
        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildRoomUtilizationAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        // Aggregated per-room summary: RoomNumber, RoomType, Total/Utilized/Free periods
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "RoomNumber", Label = "Room Number" },
            new() { Id = "RoomType", Label = "Room Type" },
            new() { Id = "TotalAvailablePeriods", Label = "Total Available Periods" },
            new() { Id = "UtilizedPeriods", Label = "Utilized Periods" },
            new() { Id = "FreePeriods", Label = "Free Periods" }
        };

        var (_, _, _, rooms, roomTypeMap, _) = await LoadLookupsAsync(includeRooms: true, includeRoomTypes: true);

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync();
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        // Count total Period slots across all timetables
        var totalPeriodSlots = timetableList.Sum(tt => tt.Slots.Count(s => s.SlotType == SlotType.Period));

        // Count how many times each room is assigned across all period slots
        var utilizedByRoom = timetableList
            .SelectMany(tt => tt.Slots.Where(s => s.SlotType == SlotType.Period))
            .SelectMany(slot => slot.Subjects.Where(s => s.RoomId.HasValue).Select(s => s.RoomId!.Value))
            .GroupBy(roomId => roomId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Filter rooms if RoomIds filter provided
        var roomIds = rooms.Keys.AsEnumerable();
        if (request.Filters.RoomIds?.Any() == true)
            roomIds = roomIds.Where(id => request.Filters.RoomIds.Contains(id));

        var rows = roomIds.OrderBy(id => rooms.GetValueOrDefault(id, ""))
            .Select(roomId =>
            {
                var utilized = utilizedByRoom.GetValueOrDefault(roomId, 0);
                var free = totalPeriodSlots - utilized;
                return new Dictionary<string, string?>
                {
                    { "RoomNumber", rooms.GetValueOrDefault(roomId, "—") },
                    { "RoomType", roomTypeMap.GetValueOrDefault(roomId, "—") },
                    { "TotalAvailablePeriods", totalPeriodSlots.ToString() },
                    { "UtilizedPeriods", utilized.ToString() },
                    { "FreePeriods", free.ToString() }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectwiseTimetableDistributionAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "TotalPeriodsAssigned", Label = "Total Periods Assigned" },
            new() { Id = "WeeklyAllocation", Label = "Weekly Allocation" },
            new() { Id = "AssignedTeachers", Label = "Assigned Teachers" }
        };

        var (standards, divisions, subjects, _, _, teachers) = await LoadLookupsAsync(
            includeStandards: true, includeDivisions: true, includeSubjects: true, includeTeachers: true);

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync();
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        // Group per (timetable, subject) → count periods, collect distinct teachers
        var groups = timetableList
            .SelectMany(tt => tt.Slots.Where(s => s.SlotType == SlotType.Period)
                .SelectMany(slot => slot.Subjects.Select(sub => new
                {
                    StandardId = tt.StandardId,
                    DivisionId = tt.DivisionId,
                    sub.SubjectId,
                    TeacherId = sub.TeacherId
                })))
            .GroupBy(x => new { x.StandardId, x.DivisionId, x.SubjectId })
            .Select(g => new
            {
                Subject = subjects.GetValueOrDefault(g.Key.SubjectId, "—"),
                Class = standards.GetValueOrDefault(g.Key.StandardId, "—"),
                Division = divisions.GetValueOrDefault(g.Key.DivisionId, "—"),
                TotalPeriods = g.Count(),
                AssignedTeachers = string.Join(", ", g
                    .Where(x => x.TeacherId.HasValue)
                    .Select(x => teachers.GetValueOrDefault(x.TeacherId!.Value, "—"))
                    .Distinct()
                    .OrderBy(t => t))
            }).ToList();

        if (request.Filters.SubjectIds?.Any() == true)
        {
            var subjectNames = request.Filters.SubjectIds.Select(id => subjects.GetValueOrDefault(id, "")).ToHashSet();
            groups = groups.Where(g => subjectNames.Contains(g.Subject)).ToList();
        }

        var rows = groups.OrderBy(g => g.Subject).ThenBy(g => g.Class).ThenBy(g => g.Division)
            .Select(g => new Dictionary<string, string?>
            {
                { "Subject", g.Subject },
                { "Class", g.Class },
                { "Division", g.Division },
                { "TotalPeriodsAssigned", g.TotalPeriods.ToString() },
                { "WeeklyAllocation", g.TotalPeriods.ToString() },
                { "AssignedTeachers", g.AssignedTeachers.Length > 0 ? g.AssignedTeachers : "—" }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildFreePeriodsAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Day", Label = "Day" },
            new() { Id = "Period", Label = "Period" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "Room", Label = "Room" },
            new() { Id = "Reason", Label = "Reason" },
            new() { Id = "Status", Label = "Status" }
        };

        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: true, includeDivisions: true);

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync();
        var timetableList = await asyncExecuter.ToListAsync(timetables.Where(t => timetableIds.Contains(t.Id)));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var tt in timetableList)
        {
            var classDiv = $"{standards.GetValueOrDefault(tt.StandardId, "—")} {divisions.GetValueOrDefault(tt.DivisionId, "")}".Trim();
            foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period && !s.Subjects.Any())
                                         .OrderBy(s => s.Day).ThenBy(s => s.Sequence))
            {
                rows.Add(new()
                {
                    { "Day", slot.Day.ToString() },
                    { "Period", $"Period {slot.Sequence} ({slot.StartTime:HH:mm}–{slot.EndTime:HH:mm})" },
                    { "Class", classDiv },
                    { "Teacher", "—" },
                    { "Room", "—" },
                    { "Reason", string.Empty },
                    { "Status", "Free" }
                });
            }
        }

        rows = rows.OrderBy(r => r["Class"]).ThenBy(r => r["Day"]).ThenBy(r => r["Period"]).ToList();
        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTimetableChangeLogAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        // Change log uses timetable-level audit fields; slot-level Change Reason / Previous/New Schedule
        // have no entity backing yet — those columns are rendered blank.
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ChangeDate", Label = "Change Date" },
            new() { Id = "ChangedBy", Label = "Changed By" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "PreviousSchedule", Label = "Previous Schedule" },
            new() { Id = "NewSchedule", Label = "New Schedule" },
            new() { Id = "ChangeReason", Label = "Change Reason" }
        };

        var (standards, divisions, _, _, _, teachers) = await LoadLookupsAsync(
            includeStandards: true, includeDivisions: true, includeTeachers: true);

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableList = await asyncExecuter.ToListAsync(
            timetableQuery.Where(t => t.LastModificationTime != null)
                          .OrderByDescending(t => t.LastModificationTime));

        var rows = timetableList.Select(tt => new Dictionary<string, string?>
        {
            { "ChangeDate", tt.LastModificationTime!.Value.ToString("dd MMM yyyy, HH:mm") },
            { "ChangedBy", tt.LastModifierId.HasValue ? teachers.GetValueOrDefault(tt.LastModifierId.Value, "—") : "—" },
            { "Class", standards.GetValueOrDefault(tt.StandardId, "—") },
            { "Division", divisions.GetValueOrDefault(tt.DivisionId, "—") },
            { "Subject", string.Empty },
            { "Teacher", string.Empty },
            { "PreviousSchedule", string.Empty },
            { "NewSchedule", string.Empty },
            { "ChangeReason", string.Empty }
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<(
        Dictionary<Guid, string> Standards,
        Dictionary<Guid, string> Divisions,
        Dictionary<Guid, string> Subjects,
        Dictionary<Guid, string> Rooms,
        Dictionary<Guid, string> RoomTypes,
        Dictionary<Guid, string> Teachers)> LoadLookupsAsync(
            bool includeStandards = false,
            bool includeDivisions = false,
            bool includeSubjects = false,
            bool includeRooms = false,
            bool includeRoomTypes = false,
            bool includeTeachers = false)
    {
        Dictionary<Guid, string> standards = [];
        Dictionary<Guid, string> divisions = [];
        Dictionary<Guid, string> subjects = [];
        Dictionary<Guid, string> rooms = [];
        Dictionary<Guid, string> roomTypeMap = [];
        Dictionary<Guid, string> teachers = [];

        if (includeStandards)
        {
            var query = await standardRepository.GetQueryableAsync();
            var names = await asyncExecuter.ToListAsync(query.Select(s => new { s.Id, s.Name }));
            standards = names.ToDictionary(s => s.Id, s => s.Name);
        }

        if (includeDivisions)
        {
            var query = await divisionRepository.GetQueryableAsync();
            var names = await asyncExecuter.ToListAsync(query.Select(d => new { d.Id, d.Name }));
            divisions = names.ToDictionary(d => d.Id, d => d.Name);
        }

        if (includeSubjects)
        {
            var query = await subjectRepository.GetQueryableAsync();
            var names = await asyncExecuter.ToListAsync(query.Select(s => new { s.Id, s.Name }));
            subjects = names.ToDictionary(s => s.Id, s => s.Name);
        }

        if (includeRooms || includeRoomTypes)
        {
            var query = await roomRepository.GetQueryableAsync();
            var roomList = await asyncExecuter.ToListAsync(query.Select(r => new { r.Id, r.RoomName, r.RoomTypeId }));
            if (includeRooms)
            {
                rooms = roomList.ToDictionary(r => r.Id, r => r.RoomName);
            }

            if (includeRoomTypes)
            {
                var typeQuery = await roomTypeRepository.GetQueryableAsync();
                var types = await asyncExecuter.ToListAsync(typeQuery.Select(rt => new { rt.Id, rt.RoomTypeName }));
                var roomTypes = types.ToDictionary(rt => rt.Id, rt => rt.RoomTypeName);
                roomTypeMap = roomList.ToDictionary(r => r.Id, r => roomTypes.GetValueOrDefault(r.RoomTypeId, "—"));
            }
        }

        if (includeTeachers)
        {
            var query = await employeeRepository.GetQueryableAsync();
            var employees = await asyncExecuter.ToListAsync(query.Select(e => new { e.Id, e.FirstName, e.LastName }));
            // Match Employee.EmployeeName, including existing whitespace in nonblank name parts.
            teachers = employees.ToDictionary(e => e.Id,
                e => string.Join(" ", new[] { e.FirstName, e.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))));
        }

        return (standards, divisions, subjects, rooms, roomTypeMap, teachers);
    }

    private async Task<Dictionary<Guid, (int Progress, DateTime? StartDate, DateTime? CompletionDate)>> LoadChapterProgressAsync(
        List<Guid> chapterIds,
        List<Guid>? divisionIds)
    {
        if (!chapterIds.Any())
            return [];

        var progressQuery = await chapterProgressRepository.GetQueryableAsync();
        progressQuery = progressQuery.Where(p => chapterIds.Contains(p.ChapterId));

        if (divisionIds?.Any() == true)
            progressQuery = progressQuery.Where(p => divisionIds.Contains(p.DivisionId));

        var progress = await asyncExecuter.ToListAsync(progressQuery
            .GroupBy(p => p.ChapterId)
            .Select(group => new
            {
                ChapterId = group.Key,
                AverageProgress = group.Average(p => p.Progress),
                StartDate = group.Min(p => p.StartDate),
                CompletionDate = group.Max(p => p.CompletionDate)
            }));

        return progress.ToDictionary(p => p.ChapterId, p => (
            Progress: (int)Math.Round(p.AverageProgress),
            p.StartDate,
            p.CompletionDate));
    }

    // ─── Exam Reports ─────────────────────────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildGenderWiseGradeAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Gender", Label = "Gender" },
            new() { Id = "StudentCount", Label = "Student Count" },
            new() { Id = "AverageMarks", Label = "Average Marks" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" },
            new() { Id = "PassPercent", Label = "Pass %" },
            new() { Id = "GradeA", Label = "Grade A (≥80%)" },
            new() { Id = "GradeB", Label = "Grade B (60–79%)" },
            new() { Id = "GradeC", Label = "Grade C (40–59%)" },
            new() { Id = "GradeD", Label = "Grade D (30–39%)" },
            new() { Id = "GradeF", Label = "Grade F (<30%)" }
        };

        if (request.Filters.ExamId is null)
        {
            payload.TotalCount = 0;
            return payload;
        }

        var examId = request.Filters.ExamId.Value;

        // Load exam with subjects for total/passing marks
        var exam = await examRepository.FindAsync(examId);
        if (exam is null)
        {
            payload.TotalCount = 0;
            return payload;
        }

        var examSubjectQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects);
        var examWithSubjects = await asyncExecuter.FirstOrDefaultAsync(examSubjectQuery.Where(e => e.Id == examId));
        var examSubjects = examWithSubjects?.ExamSubjects ?? new List<ExamSubject>();
        var totalMaxMarks = examSubjects.Sum(s => s.TotalMarks ?? 0);
        var totalPassMarks = examSubjects.Sum(s => s.PassingMarks ?? 0);

        // Load exam marks filtered by exam, optionally by class/division
        var marksQuery = await examMarkRepository.GetQueryableAsync();
        marksQuery = marksQuery.Where(m => m.ExamId == examId);

        if (request.Filters.DivisionIds?.Any() == true)
            marksQuery = marksQuery.Where(m => request.Filters.DivisionIds.Contains(m.DivisionId));

        var allMarks = await asyncExecuter.ToListAsync(marksQuery);

        if (!allMarks.Any())
        {
            payload.TotalCount = 0;
            return payload;
        }

        // Load students with gender info
        var studentIds = allMarks.Select(m => m.StudentId).Distinct().ToList();
        var studentQuery = await studentRepository.WithDetailsAsync(s => s.AdditionalDetail!);
        var students = await asyncExecuter.ToListAsync(studentQuery.Where(s => studentIds.Contains(s.Id)));
        var genderMap = students.ToDictionary(s => s.Id, s => s.AdditionalDetail?.Gender);

        // Filter by class if provided
        if (request.Filters.ClassIds?.Any() == true)
        {
            var studentIdsInClass = students
                .Where(s => s.StandardId.HasValue && request.Filters.ClassIds.Contains(s.StandardId.Value))
                .Select(s => s.Id)
                .ToHashSet();
            allMarks = allMarks.Where(m => studentIdsInClass.Contains(m.StudentId)).ToList();
        }

        // Aggregate total marks per student (sum across subjects, skip absent)
        var studentTotals = allMarks
            .GroupBy(m => m.StudentId)
            .Select(g =>
            {
                var isFullyAbsent = g.All(m => m.IsAbsent);
                var totalObtained = g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var passed = !isFullyAbsent && totalPassMarks > 0 && totalObtained >= totalPassMarks;
                var gender = genderMap.GetValueOrDefault(g.Key);
                return (StudentId: g.Key, TotalObtained: totalObtained, IsAbsent: isFullyAbsent, Passed: passed, Gender: gender);
            })
            .ToList();

        string GetGradeLabel(double obtained, double maxMarks)
        {
            if (maxMarks <= 0) return "—";
            var pct = obtained / maxMarks * 100;
            return pct >= 80 ? "A" : pct >= 60 ? "B" : pct >= 40 ? "C" : pct >= 30 ? "D" : "F";
        }

        var genderGroups = new[] { (GenderEnum?)GenderEnum.Male, (GenderEnum?)GenderEnum.Female, null };
        var rows = new List<Dictionary<string, string?>>();

        foreach (var gender in genderGroups)
        {
            IEnumerable<(Guid StudentId, double TotalObtained, bool IsAbsent, bool Passed, GenderEnum? Gender)> group;
            string genderLabel;

            if (gender is null)
            {
                group = studentTotals.Where(s => s.Gender is null);
                genderLabel = "Unknown";
            }
            else
            {
                group = studentTotals.Where(s => s.Gender == gender);
                genderLabel = gender == GenderEnum.Male ? "Male" : "Female";
            }

            var list = group.ToList();
            if (!list.Any()) continue;

            var presentStudents = list.Where(s => !s.IsAbsent).ToList();
            var count = list.Count;
            var avg = presentStudents.Any() ? Math.Round(presentStudents.Average(s => s.TotalObtained), 2) : 0;
            var highest = presentStudents.Any() ? presentStudents.Max(s => s.TotalObtained) : 0;
            var lowest = presentStudents.Any() ? presentStudents.Min(s => s.TotalObtained) : 0;
            var passCount = list.Count(s => s.Passed);
            var passPct = count > 0 ? Math.Round((double)passCount / count * 100, 2) : 0;

            var gradeA = presentStudents.Count(s => GetGradeLabel(s.TotalObtained, totalMaxMarks) == "A");
            var gradeB = presentStudents.Count(s => GetGradeLabel(s.TotalObtained, totalMaxMarks) == "B");
            var gradeC = presentStudents.Count(s => GetGradeLabel(s.TotalObtained, totalMaxMarks) == "C");
            var gradeD = presentStudents.Count(s => GetGradeLabel(s.TotalObtained, totalMaxMarks) == "D");
            var gradeF = presentStudents.Count(s => GetGradeLabel(s.TotalObtained, totalMaxMarks) == "F")
                         + list.Count(s => s.IsAbsent);

            rows.Add(new Dictionary<string, string?>
            {
                { "Gender", genderLabel },
                { "StudentCount", count.ToString() },
                { "AverageMarks", avg.ToString("F2") },
                { "HighestMarks", highest.ToString("F2") },
                { "LowestMarks", lowest.ToString("F2") },
                { "PassPercent", $"{passPct}%" },
                { "GradeA", gradeA.ToString() },
                { "GradeB", gradeB.ToString() },
                { "GradeC", gradeC.ToString() },
                { "GradeD", gradeD.ToString() },
                { "GradeF", gradeF.ToString() }
            });
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultStatisticsReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "Appeared", Label = "Appeared" },
            new() { Id = "Passed", Label = "Passed" },
            new() { Id = "Failed", Label = "Failed" },
            new() { Id = "Absent", Label = "Absent" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" },
            new() { Id = "AverageMarks", Label = "Average Marks" },
            new() { Id = "PassPercentage", Label = "Pass %" }
        };

        if (request.Filters.ExamId is null)
        {
            payload.TotalCount = 0;
            return payload;
        }

        var examId = request.Filters.ExamId.Value;

        // Load exam subjects for pass/total marks
        var examSubjectQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects);
        var examWithSubjects = await asyncExecuter.FirstOrDefaultAsync(examSubjectQuery.Where(e => e.Id == examId));
        var examSubjects = examWithSubjects?.ExamSubjects ?? new List<ExamSubject>();
        var totalPassMarks = examSubjects.Sum(s => s.PassingMarks ?? 0);

        // Load all marks for this exam
        var marksQuery = await examMarkRepository.GetQueryableAsync();
        marksQuery = marksQuery.Where(m => m.ExamId == examId);

        if (request.Filters.DivisionIds?.Any() == true)
            marksQuery = marksQuery.Where(m => request.Filters.DivisionIds.Contains(m.DivisionId));

        var allMarks = await asyncExecuter.ToListAsync(marksQuery);

        if (!allMarks.Any())
        {
            payload.TotalCount = 0;
            return payload;
        }

        // Load lookup maps
        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: true, includeDivisions: true);

        // Load students to resolve class/division names (and apply class filter)
        var studentIds = allMarks.Select(m => m.StudentId).Distinct().ToList();
        var studentQuery = await studentRepository.GetQueryableAsync();
        var studentsInExam = await asyncExecuter.ToListAsync(studentQuery.Where(s => studentIds.Contains(s.Id)));

        if (request.Filters.ClassIds?.Any() == true)
        {
            var studentIdsInClass = studentsInExam
                .Where(s => s.StandardId.HasValue && request.Filters.ClassIds.Contains(s.StandardId.Value))
                .Select(s => s.Id)
                .ToHashSet();
            allMarks = allMarks.Where(m => studentIdsInClass.Contains(m.StudentId)).ToList();
            studentsInExam = studentsInExam.Where(s => studentIdsInClass.Contains(s.Id)).ToList();
        }

        var studentStandardMap = studentsInExam.ToDictionary(s => s.Id, s => s.StandardId);
        var studentDivisionMap = studentsInExam.ToDictionary(s => s.Id, s => s.DivisionId);

        // Aggregate per student: total marks across subjects
        var studentTotals = allMarks
            .GroupBy(m => m.StudentId)
            .Select(g =>
            {
                var divisionId = g.First().DivisionId;
                var isFullyAbsent = g.All(m => m.IsAbsent);
                var totalObtained = g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var passed = !isFullyAbsent && totalPassMarks > 0 && totalObtained >= totalPassMarks;
                var standardId = studentStandardMap.GetValueOrDefault(g.Key);
                return (StudentId: g.Key, DivisionId: divisionId, StandardId: standardId, TotalObtained: totalObtained, IsAbsent: isFullyAbsent, Passed: passed);
            })
            .ToList();

        // Group by class + division for the output rows
        var grouped = studentTotals
            .GroupBy(s => (s.StandardId, s.DivisionId))
            .OrderBy(g => standards.GetValueOrDefault(g.Key.StandardId ?? Guid.Empty, ""))
            .ThenBy(g => divisions.GetValueOrDefault(g.Key.DivisionId, ""))
            .ToList();

        var rows = new List<Dictionary<string, string?>>();
        foreach (var group in grouped)
        {
            var list = group.ToList();
            var total = list.Count;
            var absent = list.Count(s => s.IsAbsent);
            var appeared = total - absent;
            var passed = list.Count(s => s.Passed);
            var failed = appeared - passed;
            var presentList = list.Where(s => !s.IsAbsent).ToList();
            var highest = presentList.Any() ? presentList.Max(s => s.TotalObtained) : 0;
            var lowest = presentList.Any() ? presentList.Min(s => s.TotalObtained) : 0;
            var avg = presentList.Any() ? Math.Round(presentList.Average(s => s.TotalObtained), 2) : 0;
            var passPct = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;
            var className = standards.GetValueOrDefault(group.Key.StandardId ?? Guid.Empty, "—");
            var divisionName = divisions.GetValueOrDefault(group.Key.DivisionId, "—");

            rows.Add(new Dictionary<string, string?>
            {
                { "ClassName", className },
                { "Division", divisionName },
                { "TotalStudents", total.ToString() },
                { "Appeared", appeared.ToString() },
                { "Passed", passed.ToString() },
                { "Failed", failed.ToString() },
                { "Absent", absent.ToString() },
                { "HighestMarks", highest.ToString("F2") },
                { "LowestMarks", lowest.ToString("F2") },
                { "AverageMarks", avg.ToString("F2") },
                { "PassPercentage", $"{passPct}%" }
            });
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows
            ? rows
            : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Shared exam data helpers ─────────────────────────────────────────────

    private static string ComputeGrade(double obtained, double total, IReadOnlyCollection<GradeRange>? gradeRanges = null)
    {
        if (total <= 0) return "—";
        if (gradeRanges?.Any() == true)
        {
            var pct = obtained / total * 100;
            var match = gradeRanges
                .OrderByDescending(r => r.MinMarks)
                .FirstOrDefault(r => pct >= r.MinMarks && pct <= r.MaxMarks);
            if (match != null) return match.Grade;
        }
        var p = obtained / total * 100;
        return p >= 90 ? "A+" : p >= 80 ? "A" : p >= 70 ? "B+" : p >= 60 ? "B"
             : p >= 50 ? "C+" : p >= 40 ? "C" : p >= 33 ? "D" : "F";
    }

    private static string BuildStudentName(Student s)
    {
        var parts = new[] { s.FirstName, s.MiddleName, s.LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts).Trim();
    }

    private static string GetRemarksFromGrade(string grade)
    {
        return grade switch
        {
            "A+" => "Excellent",
            "A" => "Very Good",
            "B+" => "Good",
            "B" => "Satisfactory",
            "C+" => "Fair",
            "C" => "Satisfactory",
            "D" => "Needs Improvement",
            "F" => "Unsatisfactory",
            _ => "—"
        };
    }

    private record ExamReportData(
        List<ExamSubject> ExamSubjects,
        double TotalMaxMarks,
        double TotalPassMarks,
        List<ExamMark> Marks,
        Dictionary<Guid, Student> StudentMap,
        Dictionary<Guid, string> SubjectNames,
        Dictionary<Guid, HashSet<Guid>> ApplicableSubjectIdsByStudent,
        IReadOnlyCollection<GradeRange>? DefaultGradeRanges);

    private async Task<ExamReportData?> LoadExamReportDataAsync(Guid examId, AcademicReportFilterDto filters)
    {
        var examSubjectQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects);
        var examWithSubjects = await asyncExecuter.FirstOrDefaultAsync(examSubjectQuery.Where(e => e.Id == examId));
        if (examWithSubjects is null) return null;

        var examSubjects = examWithSubjects.ExamSubjects.ToList();
        var totalMaxMarks = examSubjects.Sum(s => s.TotalMarks ?? 0);
        var totalPassMarks = examSubjects.Sum(s => s.PassingMarks ?? 0);

        var marksQuery = await examMarkRepository.GetQueryableAsync();
        marksQuery = marksQuery.Where(m => m.ExamId == examId);
        if (filters.DivisionIds?.Any() == true)
            marksQuery = marksQuery.Where(m => filters.DivisionIds.Contains(m.DivisionId));
        var marks = await asyncExecuter.ToListAsync(marksQuery);

        var studentIds = marks.Select(m => m.StudentId).Distinct().ToList();
        var studentQuery = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQuery.Where(s => studentIds.Contains(s.Id)));

        if (filters.ClassIds?.Any() == true)
        {
            var idsInClass = studentList
                .Where(s => s.StandardId.HasValue && filters.ClassIds.Contains(s.StandardId.Value))
                .Select(s => s.Id)
                .ToHashSet();
            marks = marks.Where(m => idsInClass.Contains(m.StudentId)).ToList();
            studentList = studentList.Where(s => idsInClass.Contains(s.Id)).ToList();
        }

        var studentMap = studentList.ToDictionary(s => s.Id);
        var applicableSubjectIdsByStudent = await subjectApplicabilityResolver.ResolveAsync(
            examWithSubjects,
            studentList);

        var subjectIds = examSubjects.Select(s => s.SubjectId).ToList();
        var subjectQuery = await subjectRepository.GetQueryableAsync();
        var subjectList = await asyncExecuter.ToListAsync(subjectQuery.Where(s => subjectIds.Contains(s.Id)));
        var subjectNames = subjectList.ToDictionary(s => s.Id, s => s.Name);

        // Load default grading scale from the overall exam
        var defaultScaleId = examWithSubjects.GradingScaleId;
        IReadOnlyCollection<GradeRange>? defaultGradeRanges = null;
        if (defaultScaleId.HasValue)
        {
            var scaleQuery = await gradingScaleRepository.WithDetailsAsync(s => s.GradeRanges);
            var scale = await asyncExecuter.FirstOrDefaultAsync(scaleQuery.Where(s => s.Id == defaultScaleId.Value));
            defaultGradeRanges = scale?.GradeRanges?.ToList()?.AsReadOnly();
        }

        return new ExamReportData(
            examSubjects,
            totalMaxMarks,
            totalPassMarks,
            marks,
            studentMap,
            subjectNames,
            applicableSubjectIdsByStudent,
            defaultGradeRanges);
    }

    // ─── Standard Result Lists ────────────────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildResultListAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "RollNo", Label = "Roll No" },
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "ResultStatus", Label = "Result Status" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var rows = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var student = data.StudentMap[g.Key];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = data.TotalMaxMarks > 0 ? Math.Round(obtained / data.TotalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                var status = isAbsent ? "Absent" : obtained >= data.TotalPassMarks ? "Pass" : "Fail";
                return new Dictionary<string, string?>
                {
                    { "RollNo", student.RollNo?.ToString() ?? "—" },
                    { "StudentName", BuildStudentName(student) },
                    { "TotalMarks", data.TotalMaxMarks.ToString("F0") },
                    { "ObtainedMarks", isAbsent ? "Absent" : obtained.ToString("F2") },
                    { "Percentage", isAbsent ? "—" : $"{pct}%" },
                    { "Grade", grade },
                    { "ResultStatus", status }
                };
            })
            .OrderBy(r => r["RollNo"])
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildGradewiseResultListAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "RollNo", Label = "Roll No" },
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Rank", Label = "Rank" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var studentResults = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var student = data.StudentMap[g.Key];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = data.TotalMaxMarks > 0 ? Math.Round(obtained / data.TotalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                return (student, pct, grade);
            })
            .OrderByDescending(x => x.pct)
            .ToList();

        // Filter to requested grade
        var targetGrade = request.Filters.Grade;
        var filtered = string.IsNullOrWhiteSpace(targetGrade)
            ? studentResults
            : studentResults.Where(x => x.grade.Equals(targetGrade, StringComparison.OrdinalIgnoreCase)).ToList();

        var rows = filtered.Select((x, i) => new Dictionary<string, string?>
        {
            { "RollNo", x.student.RollNo?.ToString() ?? "—" },
            { "StudentName", BuildStudentName(x.student) },
            { "Percentage", $"{x.pct}%" },
            { "Grade", x.grade },
            { "Rank", (i + 1).ToString() }
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildMarksRangeResultListAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Marks", Label = "Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var min = request.Filters.MinMarks ?? 0;
        var max = request.Filters.MaxMarks ?? double.MaxValue;

        var rows = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var student = data.StudentMap[g.Key];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = data.TotalMaxMarks > 0 ? Math.Round(obtained / data.TotalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                return (student, obtained, pct, grade, isAbsent);
            })
            .Where(x => !x.isAbsent && x.obtained >= min && x.obtained <= max)
            .OrderByDescending(x => x.obtained)
            .Select(x => new Dictionary<string, string?>
            {
                { "StudentName", BuildStudentName(x.student) },
                { "Marks", x.obtained.ToString("F2") },
                { "Percentage", $"{x.pct}%" },
                { "Grade", x.grade }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectwiseMarksReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "MaximumMarks", Label = "Maximum Marks" },
            new() { Id = "Grade", Label = "Grade" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        // Filter by subject if requested
        var marksToUse = data.Marks.AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        // Pre-compute per-student totals for the "Total Marks" column
        var studentTotals = data.Marks
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0));

        var rows = marksToUse
            .Where(m => data.StudentMap.ContainsKey(m.StudentId))
            .OrderBy(m => data.StudentMap[m.StudentId].RollNo)
            .ThenBy(m => data.SubjectNames.GetValueOrDefault(m.SubjectId, ""))
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var maxForSubject = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var studentTotal = studentTotals.GetValueOrDefault(m.StudentId, 0);
                var obtained = m.IsAbsent ? 0 : (m.MarksObtained ?? 0);
                var grade = m.IsAbsent ? "—" : ComputeGrade(obtained, maxForSubject, data.DefaultGradeRanges);
                return new Dictionary<string, string?>
                {
                    { "StudentName", BuildStudentName(student) },
                    { "Subject", subjectName },
                    { "TotalMarks", studentTotal.ToString("F2") },
                    { "ObtainedMarks", m.IsAbsent ? "Absent" : obtained.ToString("F2") },
                    { "MaximumMarks", maxForSubject.ToString("F0") },
                    { "Grade", grade }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildDetailedResultSheetAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        // Dynamic pivot columns: Roll No, Student Name, [SubjectName...], Total, Percentage, Grade
        var orderedSubjects = data.ExamSubjects
            .Select(es => (es.SubjectId, Name: data.SubjectNames.GetValueOrDefault(es.SubjectId, "—")))
            .OrderBy(x => x.Name)
            .ToList();

        var columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "RollNo", Label = "Roll No" },
            new() { Id = "StudentName", Label = "Student Name" }
        };
        foreach (var (subjectId, name) in orderedSubjects)
            columns.Add(new() { Id = $"subj_{subjectId}", Label = name });
        columns.Add(new() { Id = "Total", Label = "Total" });
        columns.Add(new() { Id = "Percentage", Label = "Percentage" });
        columns.Add(new() { Id = "Grade", Label = "Grade" });
        columns.Add(new() { Id = "Status", Label = "Status" });
        payload.Columns = columns;

        // Index marks by (StudentId, SubjectId)
        var markIndex = data.Marks
            .GroupBy(m => (m.StudentId, m.SubjectId))
            .ToDictionary(g => g.Key, g => g.First());

        var rows = data.StudentMap.Values
            .OrderBy(s => s.RollNo)
            .Select(student =>
            {
                var applicableSubjectIds = data.ApplicableSubjectIdsByStudent[student.Id];
                var applicableExamSubjects = data.ExamSubjects
                    .Where(subject => applicableSubjectIds.Contains(subject.SubjectId))
                    .ToList();
                var studentTotalMaxMarks = applicableExamSubjects.Sum(subject => subject.TotalMarks ?? 0);
                var addressParts = new[] { student.ContactDetail?.Address1, student.ContactDetail?.Address2, student.ContactDetail?.City, student.ContactDetail?.Pincode }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                var address = addressParts.Any() ? string.Join(", ", addressParts) : "—";

                var row = new Dictionary<string, string?>
                {
                    { "StudentId", student.Id.ToString() },
                    { "RollNo", student.RollNo?.ToString() ?? "—" },
                    { "StudentName", BuildStudentName(student) },
                    { "GrNo", student.GrNo?.ToString() ?? "—" },
                    { "AdmissionNo", student.GrNo?.ToString() ?? "—" },
                    { "FatherName", student.FamilyDetail?.FatherDetails?.FullName ?? "—" },
                    { "MotherName", student.FamilyDetail?.MotherDetails?.FullName ?? "—" },
                    { "DateOfBirth", student.AdditionalDetail?.DateOfBirth?.ToString("dd MMM yyyy") ?? "—" },
                    { "ContactNo", student.ContactDetail?.PrimaryContactNo ?? "—" },
                    { "Email", student.ContactDetail?.Email ?? "—" },
                    { "Address", address },
                    { "Session", student.AcademicYear?.Name ?? "—" },
                    { "TotalMaxMarks", studentTotalMaxMarks.ToString("F2") }
                };

                double total = 0;
                var hasFailedSubject = false;
                foreach (var (subjectId, _) in orderedSubjects)
                {
                    if (!applicableSubjectIds.Contains(subjectId))
                    {
                        row[$"subj_{subjectId}"] = "—";
                        row[$"subj_{subjectId}_Max"] = "—";
                        row[$"subj_{subjectId}_Grade"] = "—";
                        row[$"subj_{subjectId}_Remarks"] = "Not applicable";
                        continue;
                    }

                    markIndex.TryGetValue((student.Id, subjectId), out var mark);
                    var val = mark is null ? "—" : mark.IsAbsent ? "Ab" : (mark.MarksObtained ?? 0).ToString("F2");
                    if (mark is { IsAbsent: false })
                        total += mark.MarksObtained ?? 0;
                    row[$"subj_{subjectId}"] = val;

                    var examSubject = data.ExamSubjects.FirstOrDefault(es => es.SubjectId == subjectId);
                    var maxMarks = examSubject?.TotalMarks ?? 100;
                    var passingMarks = examSubject?.PassingMarks ?? (maxMarks * 0.33);
                    row[$"subj_{subjectId}_Max"] = maxMarks.ToString("F2");

                    if (mark is { IsAbsent: false } && mark.MarksObtained.HasValue)
                    {
                        var subjectGrade = ComputeGrade(mark.MarksObtained.Value, maxMarks, data.DefaultGradeRanges);
                        row[$"subj_{subjectId}_Grade"] = subjectGrade;
                        row[$"subj_{subjectId}_Remarks"] = GetRemarksFromGrade(subjectGrade);
                        if (mark.MarksObtained.Value < passingMarks)
                            hasFailedSubject = true;
                    }
                    else
                    {
                        hasFailedSubject = true;
                        row[$"subj_{subjectId}_Grade"] = "—";
                        row[$"subj_{subjectId}_Remarks"] = mark is { IsAbsent: true } ? "Absent" : "—";
                    }
                }

                var pct = studentTotalMaxMarks > 0 ? Math.Round(total / studentTotalMaxMarks * 100, 2) : 0;
                row["Total"] = total.ToString("F2");
                row["Percentage"] = $"{pct}%";
                row["Grade"] = ComputeGrade(total, studentTotalMaxMarks, data.DefaultGradeRanges);
                row["Status"] = hasFailedSubject ? "Failed" : "Passed";
                return row;
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildExamSummaryReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "Appeared", Label = "Appeared" },
            new() { Id = "Passed", Label = "Passed" },
            new() { Id = "Failed", Label = "Failed" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" },
            new() { Id = "AverageMarks", Label = "Average Marks" },
            new() { Id = "PassPercentage", Label = "Pass Percentage" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var studentTotals = data.Marks
            .GroupBy(m => m.StudentId)
            .Select(g =>
            {
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var passed = !isAbsent && obtained >= data.TotalPassMarks;
                return (isAbsent, obtained, passed);
            }).ToList();

        var total = studentTotals.Count;
        var absent = studentTotals.Count(s => s.isAbsent);
        var appeared = total - absent;
        var passed = studentTotals.Count(s => s.passed);
        var failed = appeared - passed;
        var presentList = studentTotals.Where(s => !s.isAbsent).Select(s => s.obtained).ToList();
        var highest = presentList.Any() ? presentList.Max() : 0;
        var lowest = presentList.Any() ? presentList.Min() : 0;
        var avg = presentList.Any() ? Math.Round(presentList.Average(), 2) : 0;
        var passPct = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;

        var rows = new List<Dictionary<string, string?>>
        {
            new()
            {
                { "TotalStudents", total.ToString() },
                { "Appeared", appeared.ToString() },
                { "Passed", passed.ToString() },
                { "Failed", failed.ToString() },
                { "HighestMarks", highest.ToString("F2") },
                { "LowestMarks", lowest.ToString("F2") },
                { "AverageMarks", avg.ToString("F2") },
                { "PassPercentage", $"{passPct}%" }
            }
        };

        payload.TotalCount = 1;
        payload.Page = 1;
        payload.PageSize = 1;
        payload.Rows = rows;
        return payload;
    }

    // ─── Subject Performance & Analysis ─────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildAverageSubjectMarkAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" },
            new() { Id = "AverageMarks", Label = "Average Marks" },
            new() { Id = "PassPercentage", Label = "Pass %" },
            new() { Id = "FailPercentage", Label = "Fail %" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .GroupBy(m => m.SubjectId)
            .OrderBy(g => data.SubjectNames.GetValueOrDefault(g.Key, ""))
            .Select(g =>
            {
                var subjectName = data.SubjectNames.GetValueOrDefault(g.Key, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(g.Key, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(g.Key, 0);
                var marksList = g.Select(m => m.MarksObtained ?? 0).ToList();
                var count = marksList.Count;
                var highest = marksList.Max();
                var lowest = marksList.Min();
                var avg = Math.Round(marksList.Average(), 2);
                var passed = marksList.Count(v => v >= passMarks);
                var failed = count - passed;
                var passPct = count > 0 ? Math.Round((double)passed / count * 100, 2) : 0;
                var failPct = count > 0 ? Math.Round((double)failed / count * 100, 2) : 0;
                return new Dictionary<string, string?>
                {
                    { "Subject", subjectName },
                    { "TotalStudents", count.ToString() },
                    { "TotalMarks", maxMarks.ToString("F0") },
                    { "HighestMarks", highest.ToString("F2") },
                    { "LowestMarks", lowest.ToString("F2") },
                    { "AverageMarks", avg.ToString("F2") },
                    { "PassPercentage", $"{passPct}%" },
                    { "FailPercentage", $"{failPct}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectAverageClassMarkAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ClassName", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "AverageMarks", Label = "Average Marks" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" },
            new() { Id = "PassPercentage", Label = "Pass Percentage" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var examQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects);
        var exam = await asyncExecuter.FirstOrDefaultAsync(examQuery.Where(e => e.Id == request.Filters.ExamId.Value));
        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: true, includeDivisions: true);
        var className = exam is not null ? standards.GetValueOrDefault(exam.StandardId, "—") : "—";

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .GroupBy(m => (m.DivisionId, m.SubjectId))
            .OrderBy(g => divisions.GetValueOrDefault(g.Key.DivisionId, ""))
            .ThenBy(g => data.SubjectNames.GetValueOrDefault(g.Key.SubjectId, ""))
            .Select(g =>
            {
                var divisionName = divisions.GetValueOrDefault(g.Key.DivisionId, "—");
                var subjectName = data.SubjectNames.GetValueOrDefault(g.Key.SubjectId, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(g.Key.SubjectId, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(g.Key.SubjectId, 0);
                var marksList = g.Select(m => m.MarksObtained ?? 0).ToList();
                var count = marksList.Count;
                var avg = Math.Round(marksList.Average(), 2);
                var highest = marksList.Max();
                var lowest = marksList.Min();
                var passed = marksList.Count(v => v >= passMarks);
                var passPct = count > 0 ? Math.Round((double)passed / count * 100, 2) : 0;
                return new Dictionary<string, string?>
                {
                    { "ClassName", className },
                    { "Division", divisionName },
                    { "Subject", subjectName },
                    { "TotalMarks", maxMarks.ToString("F0") },
                    { "AverageMarks", avg.ToString("F2") },
                    { "HighestMarks", highest.ToString("F2") },
                    { "LowestMarks", lowest.ToString("F2") },
                    { "PassPercentage", $"{passPct}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildStudentGradeAnalysisSubjectWiseAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "StudentCount", Label = "Student Count" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var marksWithGrade = marksToUse.Select(m =>
        {
            var maxMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
            var obtained = m.MarksObtained ?? 0;
            return (obtained, grade: ComputeGrade(obtained, maxMarks, data.DefaultGradeRanges), maxMarks);
        }).ToList();

        var total = marksWithGrade.Count;
        if (total == 0) { payload.TotalCount = 0; return payload; }

        var gradeOrder = new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F" };
        var rows = gradeOrder
            .Select(g =>
            {
                var group = marksWithGrade.Where(x => x.grade == g).ToList();
                if (group.Count == 0) return null;
                var pct = Math.Round((double)group.Count / total * 100, 2);
                var maxMarks = group.First().maxMarks;
                return new Dictionary<string, string?>
                {
                    { "Grade", g },
                    { "StudentCount", group.Count.ToString() },
                    { "Percentage", $"{pct}%" },
                    { "TotalMarks", maxMarks.ToString("F0") },
                    { "HighestMarks", group.Max(x => x.obtained).ToString("F2") },
                    { "LowestMarks", group.Min(x => x.obtained).ToString("F2") }
                };
            })
            .Where(r => r is not null).Select(r => r!).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectWiseGradeListAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Marks", Label = "Marks" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "ResultStatus", Label = "Result Status" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);
        var targetGrade = request.Filters.Grade;

        var marksToUse = data.Marks
            .Where(m => data.StudentMap.ContainsKey(m.StudentId) && !m.IsAbsent)
            .AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(m.SubjectId, 0);
                var obtained = m.MarksObtained ?? 0;
                var grade = ComputeGrade(obtained, maxMarks, data.DefaultGradeRanges);
                var status = obtained >= passMarks ? "Pass" : "Fail";
                return (student, subjectName, obtained, grade, status);
            })
            .Where(x => string.IsNullOrWhiteSpace(targetGrade) || x.grade.Equals(targetGrade, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => BuildStudentName(x.student))
            .ThenBy(x => x.subjectName)
            .Select(x => new Dictionary<string, string?>
            {
                { "StudentName", BuildStudentName(x.student) },
                { "Subject", x.subjectName },
                { "Marks", x.obtained.ToString("F2") },
                { "Grade", x.grade },
                { "ResultStatus", x.status }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListPassFailAnalysisSubjectWiseAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "Passed", Label = "Passed" },
            new() { Id = "Failed", Label = "Failed" },
            new() { Id = "PassPercentage", Label = "Pass %" },
            new() { Id = "FailPercentage", Label = "Fail %" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .GroupBy(m => m.SubjectId)
            .OrderBy(g => data.SubjectNames.GetValueOrDefault(g.Key, ""))
            .Select(g =>
            {
                var subjectName = data.SubjectNames.GetValueOrDefault(g.Key, "—");
                var passMarks = subjectPassMap.GetValueOrDefault(g.Key, 0);
                var marksList = g.ToList();
                var total = marksList.Count;
                var passed = marksList.Count(m => (m.MarksObtained ?? 0) >= passMarks);
                var failed = total - passed;
                var passPct = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;
                var failPct = total > 0 ? Math.Round((double)failed / total * 100, 2) : 0;
                return new Dictionary<string, string?>
                {
                    { "Subject", subjectName },
                    { "TotalStudents", total.ToString() },
                    { "Passed", passed.ToString() },
                    { "Failed", failed.ToString() },
                    { "PassPercentage", $"{passPct}%" },
                    { "FailPercentage", $"{failPct}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListSubjectGradeAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        // Columns: Subject + one column per grade label
        var gradeLabels = new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F" };
        payload.Columns = new List<AcademicReportColumnDto> { new() { Id = "Subject", Label = "Subject" } };
        foreach (var g in gradeLabels)
            payload.Columns.Add(new() { Id = $"Grade_{g.Replace("+", "Plus")}", Label = g });

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .GroupBy(m => m.SubjectId)
            .OrderBy(g => data.SubjectNames.GetValueOrDefault(g.Key, ""))
            .Select(g =>
            {
                var subjectName = data.SubjectNames.GetValueOrDefault(g.Key, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(g.Key, 0);
                var gradeCounts = g
                    .GroupBy(m => ComputeGrade(m.MarksObtained ?? 0, maxMarks, data.DefaultGradeRanges))
                    .ToDictionary(x => x.Key, x => x.Count());

                var row = new Dictionary<string, string?> { { "Subject", subjectName } };
                foreach (var gl in gradeLabels)
                    row[$"Grade_{gl.Replace("+", "Plus")}"] = gradeCounts.GetValueOrDefault(gl, 0).ToString();
                return row;
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListWithSubjectAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "RollNo", Label = "Roll No" },
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Marks", Label = "Marks" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Result", Label = "Result" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var rows = data.Marks
            .Where(m => data.StudentMap.ContainsKey(m.StudentId))
            .OrderBy(m => data.StudentMap[m.StudentId].RollNo)
            .ThenBy(m => data.SubjectNames.GetValueOrDefault(m.SubjectId, ""))
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(m.SubjectId, 0);
                var obtained = m.IsAbsent ? 0 : (m.MarksObtained ?? 0);
                var grade = m.IsAbsent ? "—" : ComputeGrade(obtained, maxMarks, data.DefaultGradeRanges);
                var result = m.IsAbsent ? "Absent" : obtained >= passMarks ? "Pass" : "Fail";
                return new Dictionary<string, string?>
                {
                    { "RollNo", student.RollNo?.ToString() ?? "—" },
                    { "StudentName", BuildStudentName(student) },
                    { "Subject", subjectName },
                    { "Marks", m.IsAbsent ? "Absent" : obtained.ToString("F2") },
                    { "Grade", grade },
                    { "Result", result }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListWithSubjectFailedDetailsAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "RollNo", Label = "Roll No" },
            new() { Id = "FailedSubject", Label = "Failed Subject" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "PassingMarks", Label = "Passing Marks" },
            new() { Id = "DeficitMarks", Label = "Deficit Marks" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var rows = data.Marks
            .Where(m => data.StudentMap.ContainsKey(m.StudentId) && !m.IsAbsent)
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(m.SubjectId, 0);
                var obtained = m.MarksObtained ?? 0;
                var deficit = Math.Max(0, passMarks - obtained);
                return (student, subjectName, maxMarks, obtained, passMarks, deficit, isFail: obtained < passMarks);
            })
            .Where(x => x.isFail)
            .OrderBy(x => BuildStudentName(x.student))
            .ThenBy(x => x.subjectName)
            .Select(x => new Dictionary<string, string?>
            {
                { "StudentName", BuildStudentName(x.student) },
                { "RollNo", x.student.RollNo?.ToString() ?? "—" },
                { "FailedSubject", x.subjectName },
                { "TotalMarks", x.maxMarks.ToString("F0") },
                { "ObtainedMarks", x.obtained.ToString("F2") },
                { "PassingMarks", x.passMarks.ToString("F0") },
                { "DeficitMarks", x.deficit.ToString("F2") }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListWithSubjectGradeAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Marks", Label = "Marks" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Rank", Label = "Rank" },
            new() { Id = "ResultStatus", Label = "Result Status" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent && data.StudentMap.ContainsKey(m.StudentId)).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        // Per-subject rank
        var subjectRankMap = marksToUse
            .GroupBy(m => m.SubjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.MarksObtained ?? 0)
                    .Select((m, i) => (m.StudentId, Rank: i + 1))
                    .ToDictionary(x => x.StudentId, x => x.Rank));

        var rows = marksToUse
            .OrderBy(m => data.SubjectNames.GetValueOrDefault(m.SubjectId, ""))
            .ThenByDescending(m => m.MarksObtained ?? 0)
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(m.SubjectId, 0);
                var obtained = m.MarksObtained ?? 0;
                var grade = ComputeGrade(obtained, maxMarks, data.DefaultGradeRanges);
                var status = obtained >= passMarks ? "Pass" : "Fail";
                var rank = subjectRankMap.TryGetValue(m.SubjectId, out var rm) && rm.TryGetValue(m.StudentId, out var r)
                    ? r.ToString() : "—";
                return new Dictionary<string, string?>
                {
                    { "StudentName", BuildStudentName(student) },
                    { "Subject", subjectName },
                    { "Marks", obtained.ToString("F2") },
                    { "Grade", grade },
                    { "Rank", rank },
                    { "ResultStatus", status }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListWithSubjectSmallAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainMarks", Label = "Obtain Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Result", Label = "Result" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var rows = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var student = data.StudentMap[g.Key];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = data.TotalMaxMarks > 0 ? Math.Round(obtained / data.TotalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                var result = isAbsent ? "Absent" : obtained >= data.TotalPassMarks ? "Pass" : "Fail";
                return new Dictionary<string, string?>
                {
                    { "StudentName", BuildStudentName(student) },
                    { "TotalMarks", data.TotalMaxMarks.ToString("F0") },
                    { "ObtainMarks", isAbsent ? "Absent" : obtained.ToString("F2") },
                    { "Percentage", isAbsent ? "—" : $"{pct}%" },
                    { "Grade", grade },
                    { "Result", result }
                };
            })
            .OrderBy(r => r["StudentName"])
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListWithSubjectWithGraceAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        // Grace Marks not in data model → shown as "—"; Final Marks = Obtained Marks
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "GraceMarks", Label = "Grace Marks" },
            new() { Id = "FinalMarks", Label = "Final Marks" },
            new() { Id = "FinalGrade", Label = "Final Grade" },
            new() { Id = "Status", Label = "Status" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.Where(m => !m.IsAbsent && data.StudentMap.ContainsKey(m.StudentId)).AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .OrderBy(m => BuildStudentName(data.StudentMap[m.StudentId]))
            .ThenBy(m => data.SubjectNames.GetValueOrDefault(m.SubjectId, ""))
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var maxMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var passMarks = subjectPassMap.GetValueOrDefault(m.SubjectId, 0);
                var obtained = m.MarksObtained ?? 0;
                var grade = ComputeGrade(obtained, maxMarks, data.DefaultGradeRanges);
                var status = obtained >= passMarks ? "Pass" : "Fail";
                return new Dictionary<string, string?>
                {
                    { "StudentName", BuildStudentName(student) },
                    { "Subject", subjectName },
                    { "ObtainedMarks", obtained.ToString("F2") },
                    { "TotalMarks", maxMarks.ToString("F0") },
                    { "GraceMarks", "—" },        // not in data model
                    { "FinalMarks", obtained.ToString("F2") },
                    { "FinalGrade", grade },
                    { "Status", status }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Student-Wise Full Result ─────────────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildStudentAllBatchResultAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        // StudentName added as first column since multiple students can be selected
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "ClassName", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "ExamName", Label = "Exam Name" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Rank", Label = "Rank" },
            new() { Id = "ResultStatus", Label = "Result Status" }
        };

        var studentIds = request.Filters.StudentIds;
        if (studentIds is null || !studentIds.Any()) { payload.TotalCount = 0; return payload; }

        // Load marks for target students
        var marksQuery = await examMarkRepository.GetQueryableAsync();
        marksQuery = marksQuery.Where(m => studentIds.Contains(m.StudentId));
        if (request.Filters.DivisionIds?.Any() == true)
            marksQuery = marksQuery.Where(m => request.Filters.DivisionIds.Contains(m.DivisionId));
        var targetMarks = await asyncExecuter.ToListAsync(marksQuery);
        if (!targetMarks.Any()) { payload.TotalCount = 0; return payload; }

        var examIds = targetMarks.Select(m => m.ExamId).Distinct().ToList();

        // Load exams with subjects
        var examQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects);
        var exams = await asyncExecuter.ToListAsync(examQuery.Where(e => examIds.Contains(e.Id)));

        // Apply optional class filter via Exam.StandardId
        if (request.Filters.ClassIds?.Any() == true)
        {
            exams = exams.Where(e => request.Filters.ClassIds.Contains(e.StandardId)).ToList();
            var validExamIds = exams.Select(e => e.Id).ToHashSet();
            targetMarks = targetMarks.Where(m => validExamIds.Contains(m.ExamId)).ToList();
            examIds = exams.Select(e => e.Id).ToList();
        }

        if (!exams.Any()) { payload.TotalCount = 0; return payload; }

        // Load all marks across those exams for rank computation
        var allMarksQuery = await examMarkRepository.GetQueryableAsync();
        var allMarks = await asyncExecuter.ToListAsync(allMarksQuery.Where(m => examIds.Contains(m.ExamId)));

        // Load target students
        var studentQuery = await studentRepository.GetQueryableAsync();
        var students = await asyncExecuter.ToListAsync(studentQuery.Where(s => studentIds.Contains(s.Id)));
        var studentMap = students.ToDictionary(s => s.Id);

        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync(includeStandards: true, includeDivisions: true);

        // Per-exam metadata
        var examMetaMap = exams.ToDictionary(e => e.Id, e => (
            ExamName: e.ExamName,
            ClassName: standards.GetValueOrDefault(e.StandardId, "—"),
            TotalMax: e.ExamSubjects.Sum(s => s.TotalMarks ?? 0),
            TotalPass: e.ExamSubjects.Sum(s => s.PassingMarks ?? 0)
        ));

        // Per-exam: rank all participants by obtained marks desc
        var examRankMap = examIds.ToDictionary(
            examId => examId,
            examId => allMarks
                .Where(m => m.ExamId == examId)
                .GroupBy(m => m.StudentId)
                .Select(g =>
                {
                    var obtained = g.All(m => m.IsAbsent) ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                    return (StudentId: g.Key, Obtained: obtained);
                })
                .OrderByDescending(x => x.Obtained)
                .Select((x, i) => (x.StudentId, Rank: i + 1))
                .ToDictionary(x => x.StudentId, x => x.Rank));

        var rows = targetMarks
            .GroupBy(m => (m.StudentId, m.ExamId))
            .Where(g => studentMap.ContainsKey(g.Key.StudentId) && examMetaMap.ContainsKey(g.Key.ExamId))
            .Select(g =>
            {
                var student = studentMap[g.Key.StudentId];
                var meta = examMetaMap[g.Key.ExamId];
                var divisionName = divisions.GetValueOrDefault(g.First().DivisionId, "—");
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = meta.TotalMax > 0 ? Math.Round(obtained / meta.TotalMax * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, meta.TotalMax);
                var status = isAbsent ? "Absent" : obtained >= meta.TotalPass ? "Pass" : "Fail";
                var rank = !isAbsent && examRankMap.TryGetValue(g.Key.ExamId, out var rm) && rm.TryGetValue(g.Key.StudentId, out var r)
                    ? r.ToString() : "—";
                return new Dictionary<string, string?>
                {
                    { "StudentName", BuildStudentName(student) },
                    { "ClassName", meta.ClassName },
                    { "Division", divisionName },
                    { "ExamName", meta.ExamName },
                    { "TotalMarks", meta.TotalMax.ToString("F0") },
                    { "ObtainedMarks", isAbsent ? "Absent" : obtained.ToString("F2") },
                    { "Percentage", isAbsent ? "—" : $"{pct}%" },
                    { "Grade", grade },
                    { "Rank", rank },
                    { "ResultStatus", status }
                };
            })
            .OrderBy(r => r["StudentName"])
            .ThenBy(r => r["ExamName"])
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildStudentAllExamResultAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "ExamName", Label = "Exam Name" },
            new() { Id = "ExamDate", Label = "Exam Date" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Rank", Label = "Rank" },
            new() { Id = "ResultStatus", Label = "Result Status" }
        };

        var studentIds = request.Filters.StudentIds;
        if (studentIds is null || !studentIds.Any()) { payload.TotalCount = 0; return payload; }

        // Load marks for target student(s)
        var marksQuery = await examMarkRepository.GetQueryableAsync();
        marksQuery = marksQuery.Where(m => studentIds.Contains(m.StudentId));
        if (request.Filters.DivisionIds?.Any() == true)
            marksQuery = marksQuery.Where(m => request.Filters.DivisionIds.Contains(m.DivisionId));
        var targetMarks = await asyncExecuter.ToListAsync(marksQuery);
        if (!targetMarks.Any()) { payload.TotalCount = 0; return payload; }

        var examIds = targetMarks.Select(m => m.ExamId).Distinct().ToList();

        // Load exams with subjects
        var examQuery = await examRepository.WithDetailsAsync(e => e.ExamSubjects);
        var exams = await asyncExecuter.ToListAsync(examQuery.Where(e => examIds.Contains(e.Id)));

        // Apply optional class filter
        if (request.Filters.ClassIds?.Any() == true)
        {
            exams = exams.Where(e => request.Filters.ClassIds.Contains(e.StandardId)).ToList();
            var validExamIds = exams.Select(e => e.Id).ToHashSet();
            targetMarks = targetMarks.Where(m => validExamIds.Contains(m.ExamId)).ToList();
            examIds = exams.Select(e => e.Id).ToList();
        }

        if (!exams.Any()) { payload.TotalCount = 0; return payload; }

        // Load all marks for rank computation
        var allMarksQuery = await examMarkRepository.GetQueryableAsync();
        var allMarks = await asyncExecuter.ToListAsync(allMarksQuery.Where(m => examIds.Contains(m.ExamId)));

        // Per-exam metadata (ExamDate = earliest subject date, empty if none set)
        var examMetaMap = exams.ToDictionary(e => e.Id, e => (
            ExamName: e.ExamName,
            ExamDate: e.ExamSubjects
                .Where(s => s.ExamDate.HasValue)
                .OrderBy(s => s.ExamDate)
                .Select(s => s.ExamDate!.Value.ToString("yyyy-MM-dd"))
                .FirstOrDefault() ?? "—",
            TotalMax: e.ExamSubjects.Sum(s => s.TotalMarks ?? 0),
            TotalPass: e.ExamSubjects.Sum(s => s.PassingMarks ?? 0)
        ));

        // Per-exam: rank all participants
        var examRankMap = examIds.ToDictionary(
            examId => examId,
            examId => allMarks
                .Where(m => m.ExamId == examId)
                .GroupBy(m => m.StudentId)
                .Select(g =>
                {
                    var obtained = g.All(m => m.IsAbsent) ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                    return (StudentId: g.Key, Obtained: obtained);
                })
                .OrderByDescending(x => x.Obtained)
                .Select((x, i) => (x.StudentId, Rank: i + 1))
                .ToDictionary(x => x.StudentId, x => x.Rank));

        var rows = targetMarks
            .GroupBy(m => (m.StudentId, m.ExamId))
            .Where(g => examMetaMap.ContainsKey(g.Key.ExamId))
            .Select(g =>
            {
                var meta = examMetaMap[g.Key.ExamId];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = meta.TotalMax > 0 ? Math.Round(obtained / meta.TotalMax * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, meta.TotalMax);
                var status = isAbsent ? "Absent" : obtained >= meta.TotalPass ? "Pass" : "Fail";
                var rank = !isAbsent && examRankMap.TryGetValue(g.Key.ExamId, out var rm) && rm.TryGetValue(g.Key.StudentId, out var r)
                    ? r.ToString() : "—";
                return new Dictionary<string, string?>
                {
                    { "ExamName", meta.ExamName },
                    { "ExamDate", meta.ExamDate },
                    { "TotalMarks", meta.TotalMax.ToString("F0") },
                    { "ObtainedMarks", isAbsent ? "Absent" : obtained.ToString("F2") },
                    { "Percentage", isAbsent ? "—" : $"{pct}%" },
                    { "Grade", grade },
                    { "Rank", rank },
                    { "ResultStatus", status }
                };
            })
            .OrderBy(r => r["ExamName"])
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Student Ranking / Bright-Weak ───────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildBrightStudentAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Rank", Label = "Rank" },
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "RollNo", Label = "Roll No" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "TotalMarks", Label = "Total Marks" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var targetGrade = request.Filters.Grade;

        var ranked = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var student = data.StudentMap[g.Key];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = data.TotalMaxMarks > 0 ? Math.Round(obtained / data.TotalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                return (student, obtained, pct, grade, isAbsent);
            })
            .Where(x => !x.isAbsent)
            .Where(x => string.IsNullOrWhiteSpace(targetGrade) || x.grade.Equals(targetGrade, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.pct)
            .ToList();

        var rows = ranked.Select((x, i) => new Dictionary<string, string?>
        {
            { "Rank", (i + 1).ToString() },
            { "StudentName", BuildStudentName(x.student) },
            { "RollNo", x.student.RollNo?.ToString() ?? "—" },
            { "Percentage", $"{x.pct}%" },
            { "Grade", x.grade },
            { "TotalMarks", x.obtained.ToString("F2") }
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildBrightStudentListSubjectWiseAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Rank", Label = "Rank" },
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Marks", Label = "Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var targetGrade = request.Filters.Grade;
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var subjectGroup in marksToUse
            .Where(m => data.StudentMap.ContainsKey(m.StudentId) && !m.IsAbsent)
            .GroupBy(m => m.SubjectId)
            .OrderBy(g => data.SubjectNames.GetValueOrDefault(g.Key, "")))
        {
            var subjectName = data.SubjectNames.GetValueOrDefault(subjectGroup.Key, "—");
            var subjectMax = subjectMaxMap.GetValueOrDefault(subjectGroup.Key, 0);

            var ranked = subjectGroup
                .Select(m =>
                {
                    var obtained = m.MarksObtained ?? 0;
                    var pct = subjectMax > 0 ? Math.Round(obtained / subjectMax * 100, 2) : 0;
                    var grade = ComputeGrade(obtained, subjectMax, data.DefaultGradeRanges);
                    return (m, obtained, pct, grade);
                })
                .Where(x => string.IsNullOrWhiteSpace(targetGrade) || x.grade.Equals(targetGrade, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.obtained)
                .ToList();

            rows.AddRange(ranked.Select((x, i) => new Dictionary<string, string?>
            {
                { "Rank", (i + 1).ToString() },
                { "StudentName", BuildStudentName(data.StudentMap[x.m.StudentId]) },
                { "Subject", subjectName },
                { "Marks", x.obtained.ToString("F2") },
                { "Percentage", $"{x.pct}%" },
                { "Grade", x.grade }
            }));
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildWeakStudentAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "FailedSubjects", Label = "Failed Subjects" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);

        var rows = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var student = data.StudentMap[g.Key];
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = data.TotalMaxMarks > 0 ? Math.Round(obtained / data.TotalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                var failedSubjects = g
                    .Where(m => !m.IsAbsent && (m.MarksObtained ?? 0) < subjectPassMap.GetValueOrDefault(m.SubjectId, 0))
                    .Select(m => data.SubjectNames.GetValueOrDefault(m.SubjectId, "—"))
                    .ToList();
                return (student, pct, grade, failedSubjects, isAbsent);
            })
            .Where(x => x.failedSubjects.Any() || x.isAbsent)
            .OrderBy(x => x.pct)
            .Select(x => new Dictionary<string, string?>
            {
                { "StudentName", BuildStudentName(x.student) },
                { "Percentage", x.isAbsent ? "Absent" : $"{x.pct}%" },
                { "Grade", x.grade },
                { "FailedSubjects", x.isAbsent ? "Absent" : string.Join(", ", x.failedSubjects) }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildWeakStudentAnalysisSubjectWiseAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "PassingMarks", Label = "Passing Marks" },
            new() { Id = "GapMarks", Label = "Gap Marks" },
            new() { Id = "Status", Label = "Status" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var subjectPassMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.PassingMarks ?? 0);
        var subjectMaxMap = data.ExamSubjects.ToDictionary(es => es.SubjectId, es => es.TotalMarks ?? 0);

        var marksToUse = data.Marks.AsEnumerable();
        if (request.Filters.SubjectIds?.Any() == true)
            marksToUse = marksToUse.Where(m => request.Filters.SubjectIds.Contains(m.SubjectId));

        var rows = marksToUse
            .Where(m => data.StudentMap.ContainsKey(m.StudentId) && !m.IsAbsent)
            .Select(m =>
            {
                var student = data.StudentMap[m.StudentId];
                var subjectName = data.SubjectNames.GetValueOrDefault(m.SubjectId, "—");
                var totalMarks = subjectMaxMap.GetValueOrDefault(m.SubjectId, 0);
                var passingMarks = subjectPassMap.GetValueOrDefault(m.SubjectId, 0);
                var obtained = m.MarksObtained ?? 0;
                var gap = Math.Max(0, passingMarks - obtained);
                var status = obtained >= passingMarks ? "Pass" : "Fail";
                return (student, subjectName, totalMarks, obtained, passingMarks, gap, status);
            })
            .Where(x => x.status == "Fail")
            .OrderBy(x => BuildStudentName(x.student))
            .ThenBy(x => x.subjectName)
            .Select(x => new Dictionary<string, string?>
            {
                { "StudentName", BuildStudentName(x.student) },
                { "Subject", x.subjectName },
                { "TotalMarks", x.totalMarks.ToString("F0") },
                { "ObtainedMarks", x.obtained.ToString("F2") },
                { "PassingMarks", x.passingMarks.ToString("F0") },
                { "GapMarks", x.gap.ToString("F2") },
                { "Status", x.status }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildStudentGradeAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "StudentCount", Label = "Student Count" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var studentTotals = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var grade = isAbsent ? "Absent" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                return (obtained, grade, isAbsent);
            }).ToList();

        var total = studentTotals.Count;
        var gradeOrder = new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F", "Absent" };

        var rows = gradeOrder
            .Select(g =>
            {
                var group = studentTotals.Where(x => x.grade == g).ToList();
                var count = group.Count;
                if (count == 0) return null;
                var pct = total > 0 ? Math.Round((double)count / total * 100, 2) : 0;
                var present = group.Where(x => !x.isAbsent).ToList();
                var highest = present.Any() ? present.Max(x => x.obtained) : 0;
                var lowest = present.Any() ? present.Min(x => x.obtained) : 0;
                return new Dictionary<string, string?>
                {
                    { "Grade", g },
                    { "StudentCount", count.ToString() },
                    { "Percentage", $"{pct}%" },
                    { "HighestMarks", present.Any() ? highest.ToString("F2") : "—" },
                    { "LowestMarks", present.Any() ? lowest.ToString("F2") : "—" }
                };
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListPassFailOverallAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "Appeared", Label = "Appeared" },
            new() { Id = "Passed", Label = "Passed" },
            new() { Id = "Failed", Label = "Failed" },
            new() { Id = "PassPercentage", Label = "Pass %" },
            new() { Id = "FailPercentage", Label = "Fail %" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var studentTotals = data.Marks
            .GroupBy(m => m.StudentId)
            .Select(g =>
            {
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var passed = !isAbsent && obtained >= data.TotalPassMarks;
                return (isAbsent, passed);
            }).ToList();

        var total = studentTotals.Count;
        var absent = studentTotals.Count(s => s.isAbsent);
        var appeared = total - absent;
        var passed = studentTotals.Count(s => s.passed);
        var failed = appeared - passed;
        var passPct = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;
        var failPct = total > 0 ? Math.Round((double)failed / total * 100, 2) : 0;

        var rows = new List<Dictionary<string, string?>>
        {
            new()
            {
                { "TotalStudents", total.ToString() },
                { "Appeared", appeared.ToString() },
                { "Passed", passed.ToString() },
                { "Failed", failed.ToString() },
                { "PassPercentage", $"{passPct}%" },
                { "FailPercentage", $"{failPct}%" }
            }
        };

        payload.TotalCount = 1;
        payload.Page = 1;
        payload.PageSize = 1;
        payload.Rows = rows;
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildResultListOverallGradeAnalysisAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows,
        ExamReportData? preloadedData = null)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "StudentCount", Label = "Student Count" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "RankRange", Label = "Rank Range" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = preloadedData ?? await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
        if (data is null || !data.Marks.Any()) { payload.TotalCount = 0; return payload; }

        var ranked = data.Marks
            .GroupBy(m => m.StudentId)
            .Where(g => data.StudentMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var grade = isAbsent ? "Absent" : ComputeGrade(obtained, data.TotalMaxMarks, data.DefaultGradeRanges);
                return (obtained, grade, isAbsent);
            })
            .OrderByDescending(x => x.obtained)
            .Select((x, i) => (x.obtained, x.grade, rank: i + 1))
            .ToList();

        var total = ranked.Count;
        var gradeOrder = new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F", "Absent" };

        var rows = gradeOrder
            .Select(g =>
            {
                var group = ranked.Where(x => x.grade == g).ToList();
                var count = group.Count;
                if (count == 0) return null;
                var pct = total > 0 ? Math.Round((double)count / total * 100, 2) : 0;
                var minRank = group.Min(x => x.rank);
                var maxRank = group.Max(x => x.rank);
                var rankRange = minRank == maxRank ? minRank.ToString() : $"{minRank}–{maxRank}";
                return new Dictionary<string, string?>
                {
                    { "Grade", g },
                    { "StudentCount", count.ToString() },
                    { "Percentage", $"{pct}%" },
                    { "RankRange", rankRange }
                };
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Homework reports ─────────────────────────────────────────────────────

    private static IEnumerable<(Guid DivisionId, DateTime DueDate)> GetHomeworkSchedules(HomeworkAssignmentEntity hw)
    {
        if (hw.DivisionSchedules.Any())
            return hw.DivisionSchedules.Select(ds => (ds.DivisionId, ds.DueDate));
        if (hw.DivisionId.HasValue && hw.DueDate.HasValue)
            return new[] { (hw.DivisionId.Value, hw.DueDate.Value) };
        return [];
    }

    private async Task<(
        List<HomeworkAssignmentEntity> Homeworks,
        Dictionary<Guid, string> Subjects,
        Dictionary<Guid, string> Standards,
        Dictionary<Guid, string> Divisions,
        Dictionary<Guid, string> TeacherByUserId,
        List<StudentSubmissionEntity> Submissions)> LoadHomeworkContextAsync(AcademicReportFilterDto filters)
    {
        var hwQuery = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);

        if (filters.FromDate.HasValue)
            hwQuery = hwQuery.Where(h => h.CreationTime >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            hwQuery = hwQuery.Where(h => h.CreationTime < filters.ToDate.Value.AddDays(1));
        if (filters.ClassIds?.Any() == true)
            hwQuery = hwQuery.Where(h => filters.ClassIds.Contains(h.StandardId));
        if (filters.SubjectIds?.Any() == true)
            hwQuery = hwQuery.Where(h => filters.SubjectIds.Contains(h.SubjectId));

        if (filters.TeacherIds?.Any() == true)
        {
            var empQ = await employeeRepository.GetQueryableAsync();
            var teacherUserIds = await asyncExecuter.ToListAsync(
                empQ.Where(e => filters.TeacherIds.Contains(e.Id) && e.UserId != null)
                    .Select(e => e.UserId!.Value));
            hwQuery = teacherUserIds.Any()
                ? hwQuery.Where(h => h.CreatorId.HasValue && teacherUserIds.Contains(h.CreatorId.Value))
                : hwQuery.Where(h => false);
        }

        var homeworks = await asyncExecuter.ToListAsync(hwQuery);

        if (filters.DivisionIds?.Any() == true)
        {
            homeworks = homeworks
                .Where(h => (h.DivisionId.HasValue && filters.DivisionIds.Contains(h.DivisionId.Value))
                         || h.DivisionSchedules.Any(ds => filters.DivisionIds.Contains(ds.DivisionId)))
                .ToList();
        }

        var subjects = (await subjectRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var standards = (await standardRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var divisions = (await divisionRepository.GetListAsync()).ToDictionary(d => d.Id, d => d.Name);

        var empQuery = await employeeRepository.GetQueryableAsync();
        var employees = await asyncExecuter.ToListAsync(empQuery.Where(e => e.UserId != null));
        var teacherByUserId = employees.ToDictionary(e => e.UserId!.Value, e => e.EmployeeName);

        var hwIds = homeworks.Select(h => h.Id).ToList();
        List<StudentSubmissionEntity> submissions = [];
        if (hwIds.Any())
        {
            var subQ = await submissionRepository.GetQueryableAsync();
            subQ = subQ.Where(s => s.HomeworkAssignmentId.HasValue && hwIds.Contains(s.HomeworkAssignmentId.Value));
            if (filters.DivisionIds?.Any() == true)
                subQ = subQ.Where(s => filters.DivisionIds.Contains(s.DivisionId));
            if (filters.StudentIds?.Any() == true)
                subQ = subQ.Where(s => filters.StudentIds.Contains(s.StudentId));
            submissions = await asyncExecuter.ToListAsync(subQ);
        }

        return (homeworks, subjects, standards, divisions, teacherByUserId, submissions);
    }

    private async Task<AcademicReportPreviewDto> BuildDailyHomeworkReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "HomeworkDate", Label = "Homework Date" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "HomeworkTitle", Label = "Homework Title" },
            new() { Id = "AssignedBy", Label = "Assigned By" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "SubmissionCount", Label = "Submission Count" },
            new() { Id = "Status", Label = "Status" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        var submissionsByHw = submissions
            .GroupBy(s => (s.HomeworkAssignmentId!.Value, s.DivisionId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load student counts per division
        var divisionIds = homeworks.SelectMany(GetHomeworkSchedules).Select(x => x.DivisionId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentCountByDiv = await asyncExecuter.ToListAsync(
            studentQ.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value))
                    .GroupBy(s => s.DivisionId!.Value)
                    .Select(g => new { DivisionId = g.Key, Count = g.Count() }));
        var studentCountMap = studentCountByDiv.ToDictionary(x => x.DivisionId, x => x.Count);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var hw in homeworks.OrderBy(h => h.CreationTime))
        {
            var className = standards.GetValueOrDefault(hw.StandardId, "—");
            var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
            var assignedBy = hw.CreatorId.HasValue ? teacherByUserId.GetValueOrDefault(hw.CreatorId.Value, "—") : "—";

            foreach (var (divId, dueDate) in GetHomeworkSchedules(hw))
            {
                var divName = divisions.GetValueOrDefault(divId, "—");
                var totalStudents = studentCountMap.GetValueOrDefault(divId, 0);
                var subs = submissionsByHw.GetValueOrDefault((hw.Id, divId), new());
                var submittedCount = subs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);

                rows.Add(new Dictionary<string, string?>
                {
                    { "HomeworkDate", hw.CreationTime.ToString("yyyy-MM-dd") },
                    { "Class", className },
                    { "Division", divName },
                    { "Subject", subjectName },
                    { "HomeworkTitle", hw.Title },
                    { "AssignedBy", assignedBy },
                    { "DueDate", dueDate.ToString("yyyy-MM-dd") },
                    { "TotalStudents", totalStudents.ToString() },
                    { "SubmissionCount", submittedCount.ToString() },
                    { "Status", hw.Status.ToString() }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildClasswiseHomeworkReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "HomeworkTitle", Label = "Homework Title" },
            new() { Id = "AssignedDate", Label = "Assigned Date" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "SubmissionRate", Label = "Submission Rate (%)" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        var submissionsByHw = submissions
            .GroupBy(s => (s.HomeworkAssignmentId!.Value, s.DivisionId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var divisionIds = homeworks.SelectMany(GetHomeworkSchedules).Select(x => x.DivisionId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentCountByDiv = await asyncExecuter.ToListAsync(
            studentQ.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value))
                    .GroupBy(s => s.DivisionId!.Value)
                    .Select(g => new { DivisionId = g.Key, Count = g.Count() }));
        var studentCountMap = studentCountByDiv.ToDictionary(x => x.DivisionId, x => x.Count);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var hw in homeworks.OrderBy(h => h.StandardId).ThenBy(h => h.SubjectId))
        {
            var className = standards.GetValueOrDefault(hw.StandardId, "—");
            var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
            var teacher = hw.CreatorId.HasValue ? teacherByUserId.GetValueOrDefault(hw.CreatorId.Value, "—") : "—";

            foreach (var (divId, dueDate) in GetHomeworkSchedules(hw))
            {
                var total = studentCountMap.GetValueOrDefault(divId, 0);
                var subs = submissionsByHw.GetValueOrDefault((hw.Id, divId), new());
                var submitted = subs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var rate = total > 0 ? Math.Round((double)submitted / total * 100, 2) : 0;

                rows.Add(new Dictionary<string, string?>
                {
                    { "Class", className },
                    { "Subject", subjectName },
                    { "HomeworkTitle", hw.Title },
                    { "AssignedDate", hw.CreationTime.ToString("yyyy-MM-dd") },
                    { "DueDate", dueDate.ToString("yyyy-MM-dd") },
                    { "Teacher", teacher },
                    { "SubmissionRate", $"{rate}%" }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildSubjectwiseHomeworkReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "StudentCount", Label = "Student Count" },
            new() { Id = "HomeworkCount", Label = "Homework Count" },
            new() { Id = "AssignedTeacher", Label = "Assigned Teacher" },
            new() { Id = "AverageSubmissionRate", Label = "Average Submission Rate" },
            new() { Id = "CompletionRate", Label = "Completion Rate" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        var submissionsByHw = submissions
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var divisionIds = homeworks.SelectMany(GetHomeworkSchedules).Select(x => x.DivisionId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentCountByDiv = await asyncExecuter.ToListAsync(
            studentQ.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value))
                    .GroupBy(s => s.DivisionId!.Value)
                    .Select(g => new { DivisionId = g.Key, Count = g.Count() }));
        var studentCountMap = studentCountByDiv.ToDictionary(x => x.DivisionId, x => x.Count);

        var rows = homeworks
            .GroupBy(h => h.SubjectId)
            .OrderBy(g => subjects.GetValueOrDefault(g.Key, ""))
            .Select(g =>
            {
                var subjectName = subjects.GetValueOrDefault(g.Key, "—");
                var hwList = g.ToList();
                var hwIds = hwList.Select(h => h.Id).ToHashSet();
                var subs = submissions.Where(s => s.HomeworkAssignmentId.HasValue && hwIds.Contains(s.HomeworkAssignmentId.Value)).ToList();
                var studentCount = subs.Select(s => s.StudentId).Distinct().Count();
                var teachers = hwList
                    .Where(h => h.CreatorId.HasValue)
                    .Select(h => teacherByUserId.GetValueOrDefault(h.CreatorId!.Value, ""))
                    .Distinct()
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                // Per-homework submission rates
                var rates = hwList.Select(hw =>
                {
                    var hwSubs = submissionsByHw.GetValueOrDefault(hw.Id, new());
                    var total = GetHomeworkSchedules(hw).Sum(x => studentCountMap.GetValueOrDefault(x.DivisionId, 0));
                    var submitted = hwSubs.Count(s => s.Status is SubmissionStatus.Submitted
                        or SubmissionStatus.Late or SubmissionStatus.Reviewed
                        or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                    return total > 0 ? (double)submitted / total * 100 : 0;
                }).ToList();

                var avgRate = rates.Any() ? Math.Round(rates.Average(), 2) : 0;
                var totalAssigned = subs.Count;
                var totalSubmitted = subs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var completionRate = totalAssigned > 0 ? Math.Round((double)totalSubmitted / totalAssigned * 100, 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "Subject", subjectName },
                    { "StudentCount", studentCount.ToString() },
                    { "HomeworkCount", hwList.Count.ToString() },
                    { "AssignedTeacher", teachers.Any() ? string.Join(", ", teachers) : "—" },
                    { "AverageSubmissionRate", $"{avgRate}%" },
                    { "CompletionRate", $"{completionRate}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTeacherwiseHomeworkReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "HomeworkAssigned", Label = "Homework Assigned" },
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "SubmissionRate", Label = "Submission Rate" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        var divisionIds = homeworks.SelectMany(GetHomeworkSchedules).Select(x => x.DivisionId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentCountByDiv = await asyncExecuter.ToListAsync(
            studentQ.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value))
                    .GroupBy(s => s.DivisionId!.Value)
                    .Select(g => new { DivisionId = g.Key, Count = g.Count() }));
        var studentCountMap = studentCountByDiv.ToDictionary(x => x.DivisionId, x => x.Count);

        // Expand: one row per (creatorId, subjectId, standardId, divisionId)
        var expanded = homeworks
            .SelectMany(hw => GetHomeworkSchedules(hw)
                .Select(s => (hw, divId: s.DivisionId, dueDate: s.DueDate)))
            .ToList();

        var rows = expanded
            .GroupBy(x => (x.hw.CreatorId, x.hw.SubjectId, x.hw.StandardId, x.divId))
            .OrderBy(g => g.Key.CreatorId.HasValue ? teacherByUserId.GetValueOrDefault(g.Key.CreatorId.Value, "") : "")
            .Select(g =>
            {
                var teacherName = g.Key.CreatorId.HasValue
                    ? teacherByUserId.GetValueOrDefault(g.Key.CreatorId.Value, "—") : "—";
                var subjectName = subjects.GetValueOrDefault(g.Key.SubjectId, "—");
                var className = standards.GetValueOrDefault(g.Key.StandardId, "—");
                var divName = divisions.GetValueOrDefault(g.Key.Item4, "—");
                var hwIds = g.Select(x => x.hw.Id).Distinct().ToHashSet();
                var total = studentCountMap.GetValueOrDefault(g.Key.Item4, 0);
                var subs = submissions.Where(s =>
                    s.HomeworkAssignmentId.HasValue && hwIds.Contains(s.HomeworkAssignmentId.Value)
                    && s.DivisionId == g.Key.Item4).ToList();
                var submitted = subs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var rate = total > 0 ? Math.Round((double)submitted / (total * hwIds.Count) * 100, 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "Teacher", teacherName },
                    { "Subject", subjectName },
                    { "Class", className },
                    { "Division", divName },
                    { "HomeworkAssigned", hwIds.Count.ToString() },
                    { "TotalStudents", total.ToString() },
                    { "SubmissionRate", $"{rate}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildStudentwiseHomeworkSummaryReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "AssignedHomework", Label = "Assigned Homework" },
            new() { Id = "Submitted", Label = "Submitted" },
            new() { Id = "Pending", Label = "Pending" },
            new() { Id = "Overdue", Label = "Overdue" },
            new() { Id = "CompletionPercent", Label = "Completion %" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var hwSubjectMap = homeworks.ToDictionary(h => h.Id, h => h.SubjectId);
        var hwStandardMap = homeworks.ToDictionary(h => h.Id, h => h.StandardId);
        var today = DateTime.UtcNow.Date;

        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.HomeworkAssignmentId.HasValue && hwSubjectMap.ContainsKey(s.HomeworkAssignmentId.Value))
            .GroupBy(s => (s.StudentId, s.HomeworkAssignmentId!.Value == Guid.Empty ? Guid.Empty
                : hwSubjectMap[s.HomeworkAssignmentId.Value]))
            .OrderBy(g => studentMap.ContainsKey(g.Key.StudentId)
                ? BuildStudentName(studentMap[g.Key.StudentId]) : "")
            .Select(g =>
            {
                if (!studentMap.TryGetValue(g.Key.StudentId, out var student)) return null;
                var subjectName = subjects.GetValueOrDefault(g.Key.Item2, "—");
                var divName = divisions.GetValueOrDefault(g.First().DivisionId, "—");
                var hwSample = homeworks.FirstOrDefault(h => h.Id == g.First().HomeworkAssignmentId!.Value);
                var className = hwSample is not null ? standards.GetValueOrDefault(hwSample.StandardId, "—") : "—";

                var total = g.Count();
                var submitted = g.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var pending = g.Count(s => (s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft)
                    && s.DueDate.Date >= today);
                var overdue = g.Count(s => (s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft)
                    && s.DueDate.Date < today);
                var completionPct = total > 0 ? Math.Round((double)submitted / total * 100, 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "StudentName", BuildStudentName(student) },
                    { "Class", className },
                    { "Division", divName },
                    { "Subject", subjectName },
                    { "AssignedHomework", total.ToString() },
                    { "Submitted", submitted.ToString() },
                    { "Pending", pending.ToString() },
                    { "Overdue", overdue.ToString() },
                    { "CompletionPercent", $"{completionPct}%" }
                };
            })
            .Where(r => r is not null).Select(r => r!).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildHomeworkCompletionStatusReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "HomeworkTitle", Label = "Homework Title" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "AssignedStudents", Label = "Assigned Students" },
            new() { Id = "Submitted", Label = "Submitted" },
            new() { Id = "Pending", Label = "Pending" },
            new() { Id = "CompletionPercent", Label = "Completion %" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        var submissionsByHw = submissions
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = homeworks
            .OrderBy(h => h.CreationTime)
            .Select(hw =>
            {
                var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
                var hwSubs = submissionsByHw.GetValueOrDefault(hw.Id, new());
                var assignedStudents = hwSubs.Select(s => s.StudentId).Distinct().Count();
                var submitted = hwSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var pending = assignedStudents - submitted;
                var completionPct = assignedStudents > 0
                    ? Math.Round((double)submitted / assignedStudents * 100, 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "HomeworkTitle", hw.Title },
                    { "Subject", subjectName },
                    { "AssignedStudents", assignedStudents.ToString() },
                    { "Submitted", submitted.ToString() },
                    { "Pending", pending < 0 ? "0" : pending.ToString() },
                    { "CompletionPercent", $"{completionPct}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildPendingHomeworkReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Homework", Label = "Homework" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "DaysPending", Label = "Days Pending" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var hwMap = homeworks.ToDictionary(h => h.Id);
        var today = DateTime.UtcNow.Date;

        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft
                        && s.HomeworkAssignmentId.HasValue && hwMap.ContainsKey(s.HomeworkAssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderBy(s => s.DueDate)
            .Select(s =>
            {
                var hw = hwMap[s.HomeworkAssignmentId!.Value];
                var student = studentMap[s.StudentId];
                var className = standards.GetValueOrDefault(hw.StandardId, "—");
                var divName = divisions.GetValueOrDefault(s.DivisionId, "—");
                var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
                var daysPending = Math.Max(0, (today - s.DueDate.Date).Days);

                return new Dictionary<string, string?>
                {
                    { "Student", BuildStudentName(student) },
                    { "Class", className },
                    { "Division", divName },
                    { "Subject", subjectName },
                    { "Homework", hw.Title },
                    { "DueDate", s.DueDate.ToString("yyyy-MM-dd") },
                    { "DaysPending", daysPending.ToString() }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildOverdueHomeworkReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Homework", Label = "Homework" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "OverdueDays", Label = "Overdue Days" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var hwMap = homeworks.ToDictionary(h => h.Id);
        var today = DateTime.UtcNow.Date;

        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft
                        && s.DueDate.Date < today
                        && s.HomeworkAssignmentId.HasValue && hwMap.ContainsKey(s.HomeworkAssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderByDescending(s => (today - s.DueDate.Date).Days)
            .Select(s =>
            {
                var hw = hwMap[s.HomeworkAssignmentId!.Value];
                var student = studentMap[s.StudentId];
                var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
                var overdueDays = (today - s.DueDate.Date).Days;

                return new Dictionary<string, string?>
                {
                    { "Student", BuildStudentName(student) },
                    { "Homework", hw.Title },
                    { "Subject", subjectName },
                    { "DueDate", s.DueDate.ToString("yyyy-MM-dd") },
                    { "OverdueDays", overdueDays.ToString() }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildHomeworkSubmissionHistoryReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "SubmissionDate", Label = "Submission Date" },
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Homework", Label = "Homework" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "SubmittedOnTime", Label = "Submitted On Time" },
            new() { Id = "SubmissionStatus", Label = "Submission Status" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var hwMap = homeworks.ToDictionary(h => h.Id);

        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.HomeworkAssignmentId.HasValue && hwMap.ContainsKey(s.HomeworkAssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderByDescending(s => s.SubmissionDate ?? DateTimeOffset.MinValue)
            .Select(s =>
            {
                var hw = hwMap[s.HomeworkAssignmentId!.Value];
                var student = studentMap[s.StudentId];
                var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
                var submittedOnTime = s.SubmissionDate.HasValue
                    ? (s.SubmissionDate.Value.UtcDateTime.Date <= s.DueDate.Date ? "Yes" : "No")
                    : "—";

                return new Dictionary<string, string?>
                {
                    { "SubmissionDate", s.SubmissionDate.HasValue
                        ? s.SubmissionDate.Value.ToString("yyyy-MM-dd") : "—" },
                    { "Student", BuildStudentName(student) },
                    { "Homework", hw.Title },
                    { "Subject", subjectName },
                    { "SubmittedOnTime", submittedOnTime },
                    { "SubmissionStatus", s.Status.ToString() }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildHomeworkWithAttachmentsReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "HomeworkTitle", Label = "Homework Title" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "AttachmentCount", Label = "Attachment Count" },
            new() { Id = "AttachmentType", Label = "Attachment Type" },
            new() { Id = "AssignedDate", Label = "Assigned Date" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, _) =
            await LoadHomeworkContextAsync(request.Filters);

        // Only homework with attachments (AttachmentsJson or legacy DocumentUrl)
        var withAttachments = homeworks
            .Where(h => !string.IsNullOrWhiteSpace(h.AttachmentsJson) || !string.IsNullOrWhiteSpace(h.DocumentUrl))
            .OrderBy(h => h.CreationTime)
            .ToList();

        if (!withAttachments.Any()) { payload.TotalCount = 0; return payload; }

        var rows = withAttachments.Select(hw =>
        {
            var subjectName = subjects.GetValueOrDefault(hw.SubjectId, "—");
            var teacher = hw.CreatorId.HasValue
                ? teacherByUserId.GetValueOrDefault(hw.CreatorId.Value, "—") : "—";
            int count;
            if (!string.IsNullOrWhiteSpace(hw.AttachmentsJson))
            {
                try
                {
                    var arr = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(hw.AttachmentsJson);
                    count = arr?.Length ?? 1;
                }
                catch { count = 1; }
            }
            else
            {
                count = 1; // legacy DocumentUrl = 1 attachment
            }

            return new Dictionary<string, string?>
            {
                { "HomeworkTitle", hw.Title },
                { "Subject", subjectName },
                { "Teacher", teacher },
                { "AttachmentCount", count.ToString() },
                { "AttachmentType", "—" },  // type not stored in model
                { "AssignedDate", hw.CreationTime.ToString("yyyy-MM-dd") }
            };
        }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildHomeworkAcknowledgementReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        // Acknowledgement tracking not in data model; columns shown but acknowledgement fields are "—"
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Homework", Label = "Homework" },
            new() { Id = "Student", Label = "Student" },
            new() { Id = "AcknowledgedBy", Label = "Acknowledged By" },
            new() { Id = "AcknowledgementDate", Label = "Acknowledgement Date" },
            new() { Id = "Status", Label = "Status" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var hwMap = homeworks.ToDictionary(h => h.Id);
        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.HomeworkAssignmentId.HasValue && hwMap.ContainsKey(s.HomeworkAssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderBy(s => s.HomeworkAssignmentId)
            .ThenBy(s => BuildStudentName(studentMap[s.StudentId]))
            .Select(s => new Dictionary<string, string?>
            {
                { "Homework", hwMap[s.HomeworkAssignmentId!.Value].Title },
                { "Student", BuildStudentName(studentMap[s.StudentId]) },
                { "AcknowledgedBy", "—" },
                { "AcknowledgementDate", "—" },
                { "Status", "—" }
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Assignment reports ───────────────────────────────────────────────────

    private static IEnumerable<(Guid DivisionId, DateTime DueDate)> GetAssignmentSchedules(AssignmentEntity a)
    {
        if (a.DivisionSchedules.Any())
            return a.DivisionSchedules.Select(ds => (ds.DivisionId, ds.DueDate));
        return new[] { (a.DivisionId, DateTime.MaxValue) };
    }

    private async Task<(
        List<AssignmentEntity> Assignments,
        Dictionary<Guid, string> Subjects,
        Dictionary<Guid, string> Standards,
        Dictionary<Guid, string> Divisions,
        Dictionary<Guid, string> TeacherByUserId,
        List<StudentSubmissionEntity> Submissions)> LoadAssignmentContextAsync(AcademicReportFilterDto filters)
    {
        var aQuery = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);

        if (filters.FromDate.HasValue)
            aQuery = aQuery.Where(a => a.CreationTime >= filters.FromDate.Value);
        if (filters.ToDate.HasValue)
            aQuery = aQuery.Where(a => a.CreationTime < filters.ToDate.Value.AddDays(1));
        if (filters.ClassIds?.Any() == true)
            aQuery = aQuery.Where(a => filters.ClassIds.Contains(a.StandardId));
        if (filters.SubjectIds?.Any() == true)
            aQuery = aQuery.Where(a => filters.SubjectIds.Contains(a.SubjectId));
        if (filters.AssignmentIds?.Any() == true)
            aQuery = aQuery.Where(a => filters.AssignmentIds.Contains(a.Id));

        if (filters.TeacherIds?.Any() == true)
        {
            var empQ = await employeeRepository.GetQueryableAsync();
            var teacherUserIds = await asyncExecuter.ToListAsync(
                empQ.Where(e => filters.TeacherIds.Contains(e.Id) && e.UserId != null)
                    .Select(e => e.UserId!.Value));
            aQuery = teacherUserIds.Any()
                ? aQuery.Where(a => a.CreatorId.HasValue && teacherUserIds.Contains(a.CreatorId.Value))
                : aQuery.Where(a => false);
        }

        var assignments = await asyncExecuter.ToListAsync(aQuery);

        if (filters.DivisionIds?.Any() == true)
        {
            assignments = assignments
                .Where(a => filters.DivisionIds.Contains(a.DivisionId)
                         || a.DivisionSchedules.Any(ds => filters.DivisionIds.Contains(ds.DivisionId)))
                .ToList();
        }

        var subjects = (await subjectRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var standards = (await standardRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var divisions = (await divisionRepository.GetListAsync()).ToDictionary(d => d.Id, d => d.Name);

        var empQuery = await employeeRepository.GetQueryableAsync();
        var employees = await asyncExecuter.ToListAsync(empQuery.Where(e => e.UserId != null));
        var teacherByUserId = employees.ToDictionary(e => e.UserId!.Value, e => e.EmployeeName);

        var aIds = assignments.Select(a => a.Id).ToList();
        List<StudentSubmissionEntity> submissions = [];
        if (aIds.Any())
        {
            var subQ = await submissionRepository.GetQueryableAsync();
            subQ = subQ.Where(s => s.AssignmentId.HasValue && aIds.Contains(s.AssignmentId.Value));
            if (filters.DivisionIds?.Any() == true)
                subQ = subQ.Where(s => filters.DivisionIds.Contains(s.DivisionId));
            if (filters.StudentIds?.Any() == true)
                subQ = subQ.Where(s => filters.StudentIds.Contains(s.StudentId));
            submissions = await asyncExecuter.ToListAsync(subQ);
        }

        return (assignments, subjects, standards, divisions, teacherByUserId, submissions);
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentIssuanceReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "AssignmentTitle", Label = "Assignment Title" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "AssignedBy", Label = "Assigned By" },
            new() { Id = "AssignedDate", Label = "Assigned Date" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "TotalStudents", Label = "Total Students" },
            new() { Id = "Status", Label = "Status" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!assignments.Any()) { payload.TotalCount = 0; return payload; }

        var divisionIds = assignments.SelectMany(GetAssignmentSchedules).Select(x => x.DivisionId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentCountByDiv = await asyncExecuter.ToListAsync(
            studentQ.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value))
                    .GroupBy(s => s.DivisionId!.Value)
                    .Select(g => new { DivisionId = g.Key, Count = g.Count() }));
        var studentCountMap = studentCountByDiv.ToDictionary(x => x.DivisionId, x => x.Count);

        var rows = new List<Dictionary<string, string?>>();
        foreach (var a in assignments.OrderBy(a => a.CreationTime))
        {
            var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
            var className = standards.GetValueOrDefault(a.StandardId, "—");
            var assignedBy = a.CreatorId.HasValue ? teacherByUserId.GetValueOrDefault(a.CreatorId.Value, "—") : "—";

            foreach (var (divId, dueDate) in GetAssignmentSchedules(a))
            {
                var divName = divisions.GetValueOrDefault(divId, "—");
                var totalStudents = studentCountMap.GetValueOrDefault(divId, 0);
                rows.Add(new Dictionary<string, string?>
                {
                    { "AssignmentTitle", a.Title },
                    { "Subject", subjectName },
                    { "Class", className },
                    { "Division", divName },
                    { "AssignedBy", assignedBy },
                    { "AssignedDate", a.CreationTime.ToString("yyyy-MM-dd") },
                    { "DueDate", dueDate == DateTime.MaxValue ? "—" : dueDate.ToString("yyyy-MM-dd") },
                    { "TotalStudents", totalStudents.ToString() },
                    { "Status", a.Status.ToString() }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentSubmissionReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "SubmissionDate", Label = "Submission Date" },
            new() { Id = "SubmissionStatus", Label = "Submission Status" },
            new() { Id = "DueDate", Label = "Due Date" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.AssignmentId.HasValue && aMap.ContainsKey(s.AssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderBy(s => s.AssignmentId)
            .ThenBy(s => BuildStudentName(studentMap[s.StudentId]))
            .Select(s =>
            {
                var a = aMap[s.AssignmentId!.Value];
                var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
                return new Dictionary<string, string?>
                {
                    { "Assignment", a.Title },
                    { "Student", BuildStudentName(studentMap[s.StudentId]) },
                    { "Subject", subjectName },
                    { "SubmissionDate", s.SubmissionDate.HasValue
                        ? s.SubmissionDate.Value.ToString("yyyy-MM-dd") : "—" },
                    { "SubmissionStatus", s.Status.ToString() },
                    { "DueDate", s.DueDate.ToString("yyyy-MM-dd") }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentEvaluationReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "EvaluatedBy", Label = "Evaluated By" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "EvaluationDate", Label = "Evaluation Date" },
            new() { Id = "StudentsEvaluated", Label = "Students Evaluated" },
            new() { Id = "PendingEvaluations", Label = "Pending Evaluations" },
            new() { Id = "Status", Label = "Status" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!assignments.Any()) { payload.TotalCount = 0; return payload; }

        var submissionsByAssignment = submissions
            .GroupBy(s => s.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = assignments
            .OrderBy(a => a.Title)
            .Select(a =>
            {
                var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
                var subs = submissionsByAssignment.GetValueOrDefault(a.Id, new());
                var evaluated = subs.Where(s => s.Status == SubmissionStatus.Reviewed).ToList();
                var pendingEval = subs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.NeedsReview or SubmissionStatus.Late);

                var evaluators = evaluated
                    .Where(s => s.ReviewedBy.HasValue)
                    .Select(s => teacherByUserId.GetValueOrDefault(s.ReviewedBy!.Value, ""))
                    .Distinct()
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();

                var latestReview = evaluated
                    .Where(s => s.ReviewedAt.HasValue)
                    .Select(s => s.ReviewedAt!.Value)
                    .DefaultIfEmpty()
                    .Max();

                return new Dictionary<string, string?>
                {
                    { "Assignment", a.Title },
                    { "EvaluatedBy", evaluators.Any() ? string.Join(", ", evaluators) : "—" },
                    { "Subject", subjectName },
                    { "EvaluationDate", latestReview == default ? "—" : latestReview.ToString("yyyy-MM-dd") },
                    { "StudentsEvaluated", evaluated.Count.ToString() },
                    { "PendingEvaluations", pendingEval.ToString() },
                    { "Status", a.Status.ToString() }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentMarksReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "ObtainedMarks", Label = "Obtained Marks" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.AssignmentId.HasValue && aMap.ContainsKey(s.AssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId)
                        && s.Marks.HasValue)
            .OrderBy(s => BuildStudentName(studentMap[s.StudentId]))
            .ThenBy(s => s.AssignmentId)
            .Select(s =>
            {
                var a = aMap[s.AssignmentId!.Value];
                var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
                var obtained = (double)(s.Marks ?? 0);
                var total = (double)(s.TotalMarks ?? a.TotalMarks);
                var pct = total > 0 ? Math.Round(obtained / total * 100, 2) : 0;
                var grade = ComputeGrade(obtained, total);
                return new Dictionary<string, string?>
                {
                    { "Student", BuildStudentName(studentMap[s.StudentId]) },
                    { "Assignment", a.Title },
                    { "Subject", subjectName },
                    { "ObtainedMarks", obtained.ToString("F2") },
                    { "TotalMarks", total.ToString("F0") },
                    { "Percentage", $"{pct}%" },
                    { "Grade", grade }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLateSubmissionReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "SubmissionDate", Label = "Submission Date" },
            new() { Id = "DelayDays", Label = "Delay Days" },
            new() { Id = "Subject", Label = "Subject" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.Status == SubmissionStatus.Late
                        || (s.SubmissionDate.HasValue && s.SubmissionDate.Value.UtcDateTime.Date > s.DueDate.Date))
            .Where(s => s.AssignmentId.HasValue && aMap.ContainsKey(s.AssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderByDescending(s => s.SubmissionDate.HasValue
                ? (s.SubmissionDate.Value.UtcDateTime.Date - s.DueDate.Date).Days : 0)
            .Select(s =>
            {
                var a = aMap[s.AssignmentId!.Value];
                var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
                var delayDays = s.SubmissionDate.HasValue
                    ? Math.Max(0, (s.SubmissionDate.Value.UtcDateTime.Date - s.DueDate.Date).Days)
                    : 0;
                return new Dictionary<string, string?>
                {
                    { "Student", BuildStudentName(studentMap[s.StudentId]) },
                    { "Assignment", a.Title },
                    { "DueDate", s.DueDate.ToString("yyyy-MM-dd") },
                    { "SubmissionDate", s.SubmissionDate.HasValue
                        ? s.SubmissionDate.Value.ToString("yyyy-MM-dd") : "—" },
                    { "DelayDays", delayDays.ToString() },
                    { "Subject", subjectName }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildMissingAssignmentReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "DueDate", Label = "Due Date" },
            new() { Id = "PendingDays", Label = "Pending Days" },
            new() { Id = "Teacher", Label = "Teacher" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var today = DateTime.UtcNow.Date;

        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft
                        && s.AssignmentId.HasValue && aMap.ContainsKey(s.AssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderBy(s => s.DueDate)
            .Select(s =>
            {
                var a = aMap[s.AssignmentId!.Value];
                var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
                var teacher = a.CreatorId.HasValue ? teacherByUserId.GetValueOrDefault(a.CreatorId.Value, "—") : "—";
                var pendingDays = Math.Max(0, (today - s.DueDate.Date).Days);
                return new Dictionary<string, string?>
                {
                    { "Student", BuildStudentName(studentMap[s.StudentId]) },
                    { "Assignment", a.Title },
                    { "Subject", subjectName },
                    { "DueDate", s.DueDate.ToString("yyyy-MM-dd") },
                    { "PendingDays", pendingDays.ToString() },
                    { "Teacher", teacher }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentFeedbackReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "FeedbackDate", Label = "Feedback Date" },
            new() { Id = "FeedbackRemarks", Label = "Feedback Remarks" },
            new() { Id = "Rating", Label = "Rating" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!submissions.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        var rows = submissions
            .Where(s => (!string.IsNullOrWhiteSpace(s.Feedback) || !string.IsNullOrWhiteSpace(s.Remarks))
                        && s.AssignmentId.HasValue && aMap.ContainsKey(s.AssignmentId.Value)
                        && studentMap.ContainsKey(s.StudentId))
            .OrderBy(s => s.ReviewedAt ?? DateTimeOffset.MaxValue)
            .Select(s =>
            {
                var a = aMap[s.AssignmentId!.Value];
                var subjectName = subjects.GetValueOrDefault(a.SubjectId, "—");
                var teacher = s.ReviewedBy.HasValue ? teacherByUserId.GetValueOrDefault(s.ReviewedBy.Value, "—") : "—";
                var remarks = !string.IsNullOrWhiteSpace(s.Feedback) ? s.Feedback
                    : !string.IsNullOrWhiteSpace(s.Remarks) ? s.Remarks : "—";
                return new Dictionary<string, string?>
                {
                    { "Student", BuildStudentName(studentMap[s.StudentId]) },
                    { "Subject", subjectName },
                    { "Assignment", a.Title },
                    { "Teacher", teacher },
                    { "FeedbackDate", s.ReviewedAt.HasValue
                        ? s.ReviewedAt.Value.ToString("yyyy-MM-dd") : "—" },
                    { "FeedbackRemarks", remarks },
                    { "Rating", "—" }    // not in data model
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentGradeDistributionReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Grade", Label = "Grade" },
            new() { Id = "Assignment", Label = "Assignment" },
            new() { Id = "StudentCount", Label = "Student Count" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "HighestMarks", Label = "Highest Marks" },
            new() { Id = "LowestMarks", Label = "Lowest Marks" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!assignments.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var gradeOrder = new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F" };

        var gradedSubs = submissions
            .Where(s => s.Marks.HasValue && s.AssignmentId.HasValue && aMap.ContainsKey(s.AssignmentId.Value))
            .Select(s =>
            {
                var a = aMap[s.AssignmentId!.Value];
                var obtained = (double)(s.Marks ?? 0);
                var total = (double)(s.TotalMarks ?? a.TotalMarks);
                return (s.StudentId, AssignmentTitle: a.Title, AssignmentId: a.Id, obtained, total,
                    grade: ComputeGrade(obtained, total));
            })
            .ToList();

        var rows = new List<Dictionary<string, string?>>();
        foreach (var aGroup in gradedSubs.GroupBy(x => x.AssignmentId))
        {
            var assignmentTitle = aGroup.First().AssignmentTitle;
            var totalInAssignment = aGroup.Count();
            foreach (var grade in gradeOrder)
            {
                var gradeGroup = aGroup.Where(x => x.grade == grade).ToList();
                if (!gradeGroup.Any()) continue;
                var pct = totalInAssignment > 0
                    ? Math.Round((double)gradeGroup.Count / totalInAssignment * 100, 2) : 0;
                rows.Add(new Dictionary<string, string?>
                {
                    { "Grade", grade },
                    { "Assignment", assignmentTitle },
                    { "StudentCount", gradeGroup.Count.ToString() },
                    { "Percentage", $"{pct}%" },
                    { "HighestMarks", gradeGroup.Max(x => x.obtained).ToString("F2") },
                    { "LowestMarks", gradeGroup.Min(x => x.obtained).ToString("F2") }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentPerformanceReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "TotalAssignments", Label = "Total Assignments" },
            new() { Id = "AverageScore", Label = "Average Score" },
            new() { Id = "HighestScore", Label = "Highest Score" },
            new() { Id = "LowestScore", Label = "Lowest Score" },
            new() { Id = "CompletionRate", Label = "Completion Rate" }
        };

        var (assignments, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!assignments.Any()) { payload.TotalCount = 0; return payload; }

        var aMap = assignments.ToDictionary(a => a.Id);
        var submissionsByAssignment = submissions
            .GroupBy(s => s.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = assignments
            .GroupBy(a => (a.StandardId, a.SubjectId))
            .OrderBy(g => standards.GetValueOrDefault(g.Key.StandardId, ""))
            .ThenBy(g => subjects.GetValueOrDefault(g.Key.SubjectId, ""))
            .Select(g =>
            {
                var className = standards.GetValueOrDefault(g.Key.StandardId, "—");
                var subjectName = subjects.GetValueOrDefault(g.Key.SubjectId, "—");
                var aList = g.ToList();

                var gradedSubs = aList
                    .SelectMany(a => submissionsByAssignment.GetValueOrDefault(a.Id, new())
                        .Where(s => s.Marks.HasValue)
                        .Select(s => new
                        {
                            obtained = (double)(s.Marks ?? 0),
                            total = (double)(s.TotalMarks ?? a.TotalMarks)
                        }))
                    .ToList();

                var allSubs = aList
                    .SelectMany(a => submissionsByAssignment.GetValueOrDefault(a.Id, new()))
                    .ToList();
                var submitted = allSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var completionRate = allSubs.Count > 0
                    ? Math.Round((double)submitted / allSubs.Count * 100, 2) : 0;

                var avgScore = gradedSubs.Any()
                    ? Math.Round(gradedSubs.Average(x => x.total > 0 ? x.obtained / x.total * 100 : 0), 2) : 0;
                var highest = gradedSubs.Any() ? gradedSubs.Max(x => x.obtained) : 0;
                var lowest = gradedSubs.Any() ? gradedSubs.Min(x => x.obtained) : 0;

                return new Dictionary<string, string?>
                {
                    { "Class", className },
                    { "Subject", subjectName },
                    { "TotalAssignments", aList.Count.ToString() },
                    { "AverageScore", $"{avgScore}%" },
                    { "HighestScore", highest.ToString("F2") },
                    { "LowestScore", lowest.ToString("F2") },
                    { "CompletionRate", $"{completionRate}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    // ─── Analytics & Monitoring reports ──────────────────────────────────────

    private static string ComputeLoadStatus(double avgPerWeek, double lowThreshold, double highThreshold)
        => avgPerWeek <= lowThreshold ? "Low" : avgPerWeek <= highThreshold ? "Medium" : "High";

    private async Task<AcademicReportPreviewDto> BuildHomeworkLoadAnalysisReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "HomeworkCount", Label = "Homework Count" },
            new() { Id = "TotalQuestionsTasks", Label = "Total Questions/Tasks" },
            new() { Id = "AvgHomeworkPerWeek", Label = "Average Homework Per Week" },
            new() { Id = "AvgCompletionRate", Label = "Average Completion Rate" },
            new() { Id = "LoadStatus", Label = "Load Status" }
        };

        var (homeworks, subjects, standards, divisions, _, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        var submissionsByHw = submissions
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Determine week span for per-week rate
        double spanWeeks;
        if (request.Filters.FromDate.HasValue && request.Filters.ToDate.HasValue)
        {
            spanWeeks = Math.Max(1, (request.Filters.ToDate.Value - request.Filters.FromDate.Value).TotalDays / 7);
        }
        else
        {
            var earliest = homeworks.Min(h => h.CreationTime);
            var latest = homeworks.Max(h => h.CreationTime);
            spanWeeks = Math.Max(1, (latest - earliest).TotalDays / 7);
        }

        // Expand homeworks to (standardId, divisionId, subjectId) rows
        var expanded = homeworks.SelectMany(hw =>
            GetHomeworkSchedules(hw).Select(s => (hw, divId: s.DivisionId))).ToList();

        var rows = expanded
            .GroupBy(x => (x.hw.StandardId, x.divId, x.hw.SubjectId))
            .OrderBy(g => standards.GetValueOrDefault(g.Key.StandardId, ""))
            .ThenBy(g => divisions.GetValueOrDefault(g.Key.divId, ""))
            .ThenBy(g => subjects.GetValueOrDefault(g.Key.SubjectId, ""))
            .Select(g =>
            {
                var className = standards.GetValueOrDefault(g.Key.StandardId, "—");
                var divName = divisions.GetValueOrDefault(g.Key.divId, "—");
                var subjectName = subjects.GetValueOrDefault(g.Key.SubjectId, "—");
                var hwIds = g.Select(x => x.hw.Id).Distinct().ToHashSet();
                var hwCount = hwIds.Count;
                var avgPerWeek = Math.Round(hwCount / spanWeeks, 2);

                // Avg completion rate per homework
                var rates = hwIds.Select(id =>
                {
                    var subs = submissionsByHw.GetValueOrDefault(id, new());
                    var divSubs = subs.Where(s => s.DivisionId == g.Key.divId).ToList();
                    var submitted = divSubs.Count(s => s.Status is SubmissionStatus.Submitted
                        or SubmissionStatus.Late or SubmissionStatus.Reviewed
                        or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                    return divSubs.Count > 0 ? (double)submitted / divSubs.Count * 100 : 0;
                }).ToList();
                var avgCompletion = rates.Any() ? Math.Round(rates.Average(), 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "Class", className },
                    { "Division", divName },
                    { "Subject", subjectName },
                    { "HomeworkCount", hwCount.ToString() },
                    { "TotalQuestionsTasks", "—" },    // not in data model
                    { "AvgHomeworkPerWeek", avgPerWeek.ToString("F2") },
                    { "AvgCompletionRate", $"{avgCompletion}%" },
                    { "LoadStatus", ComputeLoadStatus(avgPerWeek, 3, 7) }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildAssignmentLoadAnalysisReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "AssignmentCount", Label = "Assignment Count" },
            new() { Id = "TotalMarks", Label = "Total Marks" },
            new() { Id = "AvgAssignmentsPerMonth", Label = "Average Assignments Per Month" },
            new() { Id = "SubmissionRate", Label = "Submission Rate" },
            new() { Id = "LoadStatus", Label = "Load Status" }
        };

        var (assignments, subjects, standards, divisions, _, submissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!assignments.Any()) { payload.TotalCount = 0; return payload; }

        double spanMonths;
        if (request.Filters.FromDate.HasValue && request.Filters.ToDate.HasValue)
        {
            spanMonths = Math.Max(1, (request.Filters.ToDate.Value - request.Filters.FromDate.Value).TotalDays / 30.44);
        }
        else
        {
            var earliest = assignments.Min(a => a.CreationTime);
            var latest = assignments.Max(a => a.CreationTime);
            spanMonths = Math.Max(1, (latest - earliest).TotalDays / 30.44);
        }

        var submissionsByAssignment = submissions
            .GroupBy(s => s.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var expanded = assignments.SelectMany(a =>
            GetAssignmentSchedules(a).Select(s => (a, divId: s.DivisionId))).ToList();

        var rows = expanded
            .GroupBy(x => (x.a.StandardId, x.divId, x.a.SubjectId))
            .OrderBy(g => standards.GetValueOrDefault(g.Key.StandardId, ""))
            .ThenBy(g => divisions.GetValueOrDefault(g.Key.divId, ""))
            .ThenBy(g => subjects.GetValueOrDefault(g.Key.SubjectId, ""))
            .Select(g =>
            {
                var className = standards.GetValueOrDefault(g.Key.StandardId, "—");
                var divName = divisions.GetValueOrDefault(g.Key.divId, "—");
                var subjectName = subjects.GetValueOrDefault(g.Key.SubjectId, "—");
                var aList = g.Select(x => x.a).Distinct().ToList();
                var aIds = aList.Select(a => a.Id).ToHashSet();

                var totalMarks = aList.Sum(a => (double)a.TotalMarks);
                var avgPerMonth = Math.Round(aList.Count / spanMonths, 2);

                var allSubs = aList.SelectMany(a =>
                    submissionsByAssignment.GetValueOrDefault(a.Id, new())
                    .Where(s => s.DivisionId == g.Key.divId)).ToList();
                var submitted = allSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var submissionRate = allSubs.Count > 0
                    ? Math.Round((double)submitted / allSubs.Count * 100, 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "Class", className },
                    { "Division", divName },
                    { "Subject", subjectName },
                    { "AssignmentCount", aList.Count.ToString() },
                    { "TotalMarks", totalMarks.ToString("F0") },
                    { "AvgAssignmentsPerMonth", avgPerMonth.ToString("F2") },
                    { "SubmissionRate", $"{submissionRate}%" },
                    { "LoadStatus", ComputeLoadStatus(avgPerMonth, 2, 4) }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildTeacherPerformanceInHomeworkEvaluationReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Teacher", Label = "Teacher" },
            new() { Id = "HomeworkAssigned", Label = "Homework Assigned" },
            new() { Id = "HomeworkEvaluated", Label = "Homework Evaluated" },
            new() { Id = "PendingEvaluation", Label = "Pending Evaluation" },
            new() { Id = "AvgEvaluationTime", Label = "Average Evaluation Time" },
            new() { Id = "EvaluationCompletionPercent", Label = "Evaluation Completion %" }
        };

        var (homeworks, subjects, standards, divisions, teacherByUserId, submissions) =
            await LoadHomeworkContextAsync(request.Filters);

        if (!homeworks.Any()) { payload.TotalCount = 0; return payload; }

        // Group homeworks by CreatorId (teacher)
        var hwByCreator = homeworks
            .Where(h => h.CreatorId.HasValue)
            .GroupBy(h => h.CreatorId!.Value)
            .ToList();

        var hwIds = homeworks.Select(h => h.Id).ToHashSet();
        var submissionsByHw = submissions
            .Where(s => s.HomeworkAssignmentId.HasValue && hwIds.Contains(s.HomeworkAssignmentId.Value))
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = hwByCreator
            .OrderBy(g => teacherByUserId.GetValueOrDefault(g.Key, ""))
            .Select(g =>
            {
                var teacherName = teacherByUserId.GetValueOrDefault(g.Key, "—");
                var hwAssigned = g.Count();
                var allSubs = g.SelectMany(hw =>
                    submissionsByHw.GetValueOrDefault(hw.Id, new())).ToList();

                var evaluated = allSubs.Where(s => s.Status == SubmissionStatus.Reviewed
                    && s.ReviewedBy.HasValue).ToList();
                var pending = allSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.NeedsReview or SubmissionStatus.Late);

                // Avg evaluation time = avg (ReviewedAt - SubmissionDate)
                var evalTimes = evaluated
                    .Where(s => s.ReviewedAt.HasValue && s.SubmissionDate.HasValue)
                    .Select(s => (s.ReviewedAt!.Value - s.SubmissionDate!.Value).TotalHours)
                    .ToList();
                var avgEvalHours = evalTimes.Any() ? Math.Round(evalTimes.Average(), 1) : 0;
                var avgEvalTimeStr = avgEvalHours < 24
                    ? $"{avgEvalHours} hrs"
                    : $"{Math.Round(avgEvalHours / 24, 1)} days";

                var total = evaluated.Count + pending;
                var completionPct = total > 0
                    ? Math.Round((double)evaluated.Count / total * 100, 2) : 0;

                return new Dictionary<string, string?>
                {
                    { "Teacher", teacherName },
                    { "HomeworkAssigned", hwAssigned.ToString() },
                    { "HomeworkEvaluated", evaluated.Count.ToString() },
                    { "PendingEvaluation", pending.ToString() },
                    { "AvgEvaluationTime", evalTimes.Any() ? avgEvalTimeStr : "—" },
                    { "EvaluationCompletionPercent", $"{completionPct}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildStudentPerformanceTrendReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Student", Label = "Student" },
            new() { Id = "Subject", Label = "Subject" },
            new() { Id = "HomeworkAverage", Label = "Homework Average" },
            new() { Id = "AssignmentAverage", Label = "Assignment Average" },
            new() { Id = "PreviousPeriodScore", Label = "Previous Period Score" },
            new() { Id = "CurrentPeriodScore", Label = "Current Period Score" },
            new() { Id = "GrowthPercent", Label = "Growth %" }
        };

        if (request.Filters.StudentIds?.Any() != true) { payload.TotalCount = 0; return payload; }

        // Load both homework and assignment data
        var (homeworks, hwSubjects, hwStandards, hwDivisions, _, hwSubmissions) =
            await LoadHomeworkContextAsync(request.Filters);
        var (assignments, aSubjects, _, _, _, aSubmissions) =
            await LoadAssignmentContextAsync(request.Filters);

        var subjects = hwSubjects.Keys.Union(aSubjects.Keys)
            .ToDictionary(id => id, id => hwSubjects.ContainsKey(id) ? hwSubjects[id] : aSubjects[id]);

        var studentIds = request.Filters.StudentIds!;
        var studentQ = await studentRepository.GetQueryableAsync();
        var studentList = await asyncExecuter.ToListAsync(studentQ.Where(s => studentIds.Contains(s.Id)));
        var studentMap = studentList.ToDictionary(s => s.Id);

        // Split period in half if dates provided
        DateTime? midPoint = null;
        if (request.Filters.FromDate.HasValue && request.Filters.ToDate.HasValue)
            midPoint = request.Filters.FromDate.Value + (request.Filters.ToDate.Value - request.Filters.FromDate.Value) / 2;

        var hwSubjectMap = homeworks.ToDictionary(h => h.Id, h => h.SubjectId);
        var aSubjectMap = assignments.ToDictionary(a => a.Id, a => a.SubjectId);

        // Combine all subjectIds from both sources
        var allSubjectIds = hwSubmissions.Where(s => s.HomeworkAssignmentId.HasValue && hwSubjectMap.ContainsKey(s.HomeworkAssignmentId.Value))
            .Select(s => hwSubjectMap[s.HomeworkAssignmentId!.Value])
            .Union(aSubmissions.Where(s => s.AssignmentId.HasValue && aSubjectMap.ContainsKey(s.AssignmentId.Value))
            .Select(s => aSubjectMap[s.AssignmentId!.Value]))
            .Distinct().ToList();

        var rows = new List<Dictionary<string, string?>>();
        foreach (var studentId in studentIds)
        {
            if (!studentMap.TryGetValue(studentId, out var student)) continue;
            var studentName = BuildStudentName(student);

            foreach (var subjectId in allSubjectIds)
            {
                var subjectName = subjects.GetValueOrDefault(subjectId, "—");

                // Homework average (completion %) for this student+subject
                var hwSubs = hwSubmissions.Where(s => s.StudentId == studentId
                    && s.HomeworkAssignmentId.HasValue
                    && hwSubjectMap.GetValueOrDefault(s.HomeworkAssignmentId.Value) == subjectId).ToList();
                var hwSubmitted = hwSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var hwAvg = hwSubs.Count > 0 ? Math.Round((double)hwSubmitted / hwSubs.Count * 100, 2) : 0;

                // Assignment average (marks %) for this student+subject
                var aSubs = aSubmissions.Where(s => s.StudentId == studentId
                    && s.AssignmentId.HasValue
                    && aSubjectMap.GetValueOrDefault(s.AssignmentId.Value) == subjectId
                    && s.Marks.HasValue).ToList();
                var aScores = aSubs.Select(s =>
                {
                    var aEntity = assignments.FirstOrDefault(a => a.Id == s.AssignmentId);
                    var total = (double)(s.TotalMarks ?? aEntity?.TotalMarks ?? 0);
                    return total > 0 ? (double)s.Marks!.Value / total * 100 : 0;
                }).ToList();
                var aAvg = aScores.Any() ? Math.Round(aScores.Average(), 2) : 0;

                if (hwSubs.Count == 0 && aSubs.Count == 0) continue;

                // Period split scores (using assignment marks as score proxy)
                string prevScore = "—", currScore = "—", growth = "—";
                if (midPoint.HasValue && aSubs.Any())
                {
                    var prevSubs = aSubs.Where(s => s.SubmissionDate.HasValue
                        && s.SubmissionDate.Value.UtcDateTime < midPoint.Value).ToList();
                    var currSubs = aSubs.Where(s => s.SubmissionDate.HasValue
                        && s.SubmissionDate.Value.UtcDateTime >= midPoint.Value).ToList();

                    double CalcAvgScore(List<StudentSubmissionEntity> list)
                    {
                        var scores = list.Select(s =>
                        {
                            var aEntity = assignments.FirstOrDefault(a => a.Id == s.AssignmentId);
                            var total = (double)(s.TotalMarks ?? aEntity?.TotalMarks ?? 0);
                            return total > 0 ? (double)s.Marks!.Value / total * 100 : 0;
                        }).ToList();
                        return scores.Any() ? Math.Round(scores.Average(), 2) : 0;
                    }

                    var prev = CalcAvgScore(prevSubs);
                    var curr = CalcAvgScore(currSubs);
                    prevScore = prevSubs.Any() ? $"{prev}%" : "—";
                    currScore = currSubs.Any() ? $"{curr}%" : "—";
                    if (prevSubs.Any() && currSubs.Any() && prev > 0)
                        growth = $"{Math.Round((curr - prev) / prev * 100, 2)}%";
                }

                rows.Add(new Dictionary<string, string?>
                {
                    { "Student", studentName },
                    { "Subject", subjectName },
                    { "HomeworkAverage", hwSubs.Count > 0 ? $"{hwAvg}%" : "—" },
                    { "AssignmentAverage", aScores.Any() ? $"{aAvg}%" : "—" },
                    { "PreviousPeriodScore", prevScore },
                    { "CurrentPeriodScore", currScore },
                    { "GrowthPercent", growth }
                });
            }
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildClasswiseOverallHomeworkAssignmentReportAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "Class", Label = "Class" },
            new() { Id = "Division", Label = "Division" },
            new() { Id = "TotalHomework", Label = "Total Homework" },
            new() { Id = "TotalAssignments", Label = "Total Assignments" },
            new() { Id = "HomeworkCompletionPercent", Label = "Homework Completion %" },
            new() { Id = "AssignmentSubmissionPercent", Label = "Assignment Submission %" },
            new() { Id = "AverageMarks", Label = "Average Marks" },
            new() { Id = "OverallAcademicScore", Label = "Overall Academic Score" }
        };

        var (homeworks, hwSubjects, standards, divisions, _, hwSubmissions) =
            await LoadHomeworkContextAsync(request.Filters);
        var (assignments, _, _, _, _, aSubmissions) =
            await LoadAssignmentContextAsync(request.Filters);

        if (!homeworks.Any() && !assignments.Any()) { payload.TotalCount = 0; return payload; }

        var hwSubmissionsByHw = hwSubmissions
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var aSubmissionsByA = aSubmissions
            .GroupBy(s => s.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Collect all (standardId, divisionId) combinations
        var hwPairs = homeworks.SelectMany(h => GetHomeworkSchedules(h).Select(s => (h.StandardId, divId: s.DivisionId, hwId: h.Id)));
        var aPairs = assignments.SelectMany(a => GetAssignmentSchedules(a).Select(s => (a.StandardId, divId: s.DivisionId, aId: a.Id)));

        var allPairs = hwPairs.Select(x => (x.StandardId, x.divId))
            .Union(aPairs.Select(x => (x.StandardId, x.divId)))
            .Distinct().ToList();

        var rows = allPairs
            .OrderBy(p => standards.GetValueOrDefault(p.StandardId, ""))
            .ThenBy(p => divisions.GetValueOrDefault(p.divId, ""))
            .Select(p =>
            {
                var className = standards.GetValueOrDefault(p.StandardId, "—");
                var divName = divisions.GetValueOrDefault(p.divId, "—");

                var divHwIds = homeworks
                    .Where(h => h.StandardId == p.StandardId
                        && GetHomeworkSchedules(h).Any(s => s.DivisionId == p.divId))
                    .Select(h => h.Id).ToHashSet();
                var divAIds = assignments
                    .Where(a => a.StandardId == p.StandardId
                        && GetAssignmentSchedules(a).Any(s => s.DivisionId == p.divId))
                    .Select(a => a.Id).ToHashSet();

                // Homework completion
                var hwSubs = divHwIds.SelectMany(id => hwSubmissionsByHw.GetValueOrDefault(id, new())
                    .Where(s => s.DivisionId == p.divId)).ToList();
                var hwSubmitted = hwSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var hwCompletion = hwSubs.Count > 0
                    ? Math.Round((double)hwSubmitted / hwSubs.Count * 100, 2) : 0;

                // Assignment submission
                var aSubs = divAIds.SelectMany(id => aSubmissionsByA.GetValueOrDefault(id, new())
                    .Where(s => s.DivisionId == p.divId)).ToList();
                var aSubmitted = aSubs.Count(s => s.Status is SubmissionStatus.Submitted
                    or SubmissionStatus.Late or SubmissionStatus.Reviewed
                    or SubmissionStatus.Excused or SubmissionStatus.NeedsReview);
                var aSubmission = aSubs.Count > 0
                    ? Math.Round((double)aSubmitted / aSubs.Count * 100, 2) : 0;

                // Average marks (assignment marks %)
                var markedSubs = aSubs.Where(s => s.Marks.HasValue).ToList();
                var avgMarks = markedSubs.Any()
                    ? Math.Round(markedSubs.Select(s =>
                    {
                        var a = assignments.FirstOrDefault(a => a.Id == s.AssignmentId);
                        var total = (double)(s.TotalMarks ?? a?.TotalMarks ?? 0);
                        return total > 0 ? (double)s.Marks!.Value / total * 100 : 0;
                    }).Average(), 2)
                    : 0;

                var overallScore = Math.Round((hwCompletion + aSubmission + avgMarks) / 3, 2);

                return new Dictionary<string, string?>
                {
                    { "Class", className },
                    { "Division", divName },
                    { "TotalHomework", divHwIds.Count.ToString() },
                    { "TotalAssignments", divAIds.Count.ToString() },
                    { "HomeworkCompletionPercent", $"{hwCompletion}%" },
                    { "AssignmentSubmissionPercent", $"{aSubmission}%" },
                    { "AverageMarks", markedSubs.Any() ? $"{avgMarks}%" : "—" },
                    { "OverallAcademicScore", $"{overallScore}%" }
                };
            }).ToList();

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        payload.Rows = exportAllRows ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return payload;
    }
}
