using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
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

public class AcademicReportGenerator(
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
    IRepository<Student, Guid> studentRepository) : ITransientDependency
{
    public Task<AcademicReportOptionsDto> GetOptionsAsync(Guid userId, Guid? tenantId)
    {
        var dto = new AcademicReportOptionsDto
        {
            ReportTypes = new List<AcademicReportOptionDto>
            {
                new() { Id = AcademicReportTypes.ChapterProgressReport, Label = "Chapter Progress Report" },
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
                new() { Id = AcademicReportTypes.ExamSummaryReport, Label = "Exam Summary Report" }
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
        var payload = new AcademicReportPreviewDto
        {
            ReportType = request.ReportType,
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
            case AcademicReportTypes.ExamSummaryReport:
                return await BuildExamSummaryReportAsync(request, payload, exportAllRows);
            default:
                return await BuildChapterProgressReportAsync(query, request, payload, exportAllRows);
        }
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

        var (_, _, _, _, _, teachers) = await LoadLookupsAsync();
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
            var coveragePct = total == 0 ? 0 : Math.Round((double) covered / total * 100, 2);
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
                rows.Add(new Dictionary<string, string?>
                {
                    { "SubjectName", s.Subject?.Name ?? "—" },
                    { "ChapterNumber", chapter.ChapterNumber.ToString() },
                    { "ChapterName", chapter.ChapterName },
                    { "Description", chapter.Description ?? string.Empty },
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
                    delayDays = ((int) (actualEnd.Value - chapter.ChapterEndTime.DateTime).TotalDays).ToString();

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
        var (_, _, _, _, _, teachers) = await LoadLookupsAsync();
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
            var completionPct = total == 0 ? 0 : Math.Round((double) completed / total * 100, 2);
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
        var (_, _, _, _, _, teachers) = await LoadLookupsAsync();
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

        var (standards, divisions, subjects, rooms, _, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
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

        var (standards, divisions, subjects, rooms, _, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
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

        var (_, _, _, rooms, roomTypeMap, _) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
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

        var (standards, divisions, subjects, _, _, teachers) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));
        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
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

        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync();

        var timetableQuery = await timetableRepository.GetQueryableAsync();
        if (request.Filters.ClassIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.ClassIds.Contains(t.StandardId));
        if (request.Filters.DivisionIds?.Any() == true)
            timetableQuery = timetableQuery.Where(t => request.Filters.DivisionIds.Contains(t.DivisionId));

        var timetableIds = await asyncExecuter.ToListAsync(timetableQuery.Select(t => t.Id));
        var timetables = await timetableRepository.WithDetailsAsync(t => t.Slots);
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

        var (standards, divisions, _, _, _, teachers) = await LoadLookupsAsync();

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
        Dictionary<Guid, string> Teachers)> LoadLookupsAsync()
    {
        var standards = (await standardRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var divisions = (await divisionRepository.GetListAsync()).ToDictionary(d => d.Id, d => d.Name);
        var subjects = (await subjectRepository.GetListAsync()).ToDictionary(s => s.Id, s => s.Name);
        var roomList = (await roomRepository.GetListAsync());
        var rooms = roomList.ToDictionary(r => r.Id, r => r.RoomName);
        var roomTypes = (await roomTypeRepository.GetListAsync()).ToDictionary(rt => rt.Id, rt => rt.RoomTypeName);
        var roomTypeMap = roomList.ToDictionary(r => r.Id, r => roomTypes.GetValueOrDefault(r.RoomTypeId, "—"));
        var employeeQuery = await employeeRepository.GetQueryableAsync();
        var employees = await asyncExecuter.ToListAsync(employeeQuery);
        var teachers = employees.ToDictionary(e => e.Id, e => e.EmployeeName);

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

        var progressList = await asyncExecuter.ToListAsync(progressQuery);

        return progressList
            .GroupBy(p => p.ChapterId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Progress: (int) Math.Round(g.Average(p => p.Progress)),
                    StartDate: g.Where(p => p.StartDate.HasValue).Select(p => p.StartDate).Min(),
                    CompletionDate: g.Where(p => p.CompletionDate.HasValue).Select(p => p.CompletionDate).Max()
                ));
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

        var genderGroups = new[] { (GenderEnum?) GenderEnum.Male, (GenderEnum?) GenderEnum.Female, null };
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
            var passPct = count > 0 ? Math.Round((double) passCount / count * 100, 2) : 0;

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
        var (standards, divisions, _, _, _, _) = await LoadLookupsAsync();

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
            var passPct = total > 0 ? Math.Round((double) passed / total * 100, 2) : 0;
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

    private static string ComputeGrade(double obtained, double total)
    {
        if (total <= 0) return "—";
        var pct = obtained / total * 100;
        return pct >= 90 ? "A+" : pct >= 80 ? "A" : pct >= 70 ? "B+" : pct >= 60 ? "B"
             : pct >= 50 ? "C+" : pct >= 40 ? "C" : pct >= 33 ? "D" : "F";
    }

    private static string BuildStudentName(Student s)
    {
        var parts = new[] { s.FirstName, s.MiddleName, s.LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts).Trim();
    }

    private record ExamReportData(
        List<ExamSubject> ExamSubjects,
        double TotalMaxMarks,
        double TotalPassMarks,
        List<ExamMark> Marks,
        Dictionary<Guid, Student> StudentMap,
        Dictionary<Guid, string> SubjectNames);

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

        var subjectIds = examSubjects.Select(s => s.SubjectId).ToList();
        var subjectQuery = await subjectRepository.GetQueryableAsync();
        var subjectList = await asyncExecuter.ToListAsync(subjectQuery.Where(s => subjectIds.Contains(s.Id)));
        var subjectNames = subjectList.ToDictionary(s => s.Id, s => s.Name);

        return new ExamReportData(examSubjects, totalMaxMarks, totalPassMarks, marks, studentMap, subjectNames);
    }

    // ─── Standard Result Lists ────────────────────────────────────────────────

    private async Task<AcademicReportPreviewDto> BuildResultListAsync(
        AcademicReportRequestDto request,
        AcademicReportPreviewDto payload,
        bool exportAllRows)
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
        var data = await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
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
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks);
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
        bool exportAllRows)
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
        var data = await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
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
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks);
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
        bool exportAllRows)
    {
        payload.Columns = new List<AcademicReportColumnDto>
        {
            new() { Id = "StudentName", Label = "Student Name" },
            new() { Id = "Marks", Label = "Marks" },
            new() { Id = "Percentage", Label = "Percentage" },
            new() { Id = "Grade", Label = "Grade" }
        };

        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
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
                var grade = isAbsent ? "—" : ComputeGrade(obtained, data.TotalMaxMarks);
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
        bool exportAllRows)
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
        var data = await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
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
                var grade = m.IsAbsent ? "—" : ComputeGrade(obtained, maxForSubject);
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
        bool exportAllRows)
    {
        if (request.Filters.ExamId is null) { payload.TotalCount = 0; return payload; }
        var data = await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
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
        payload.Columns = columns;

        // Index marks by (StudentId, SubjectId)
        var markIndex = data.Marks
            .GroupBy(m => (m.StudentId, m.SubjectId))
            .ToDictionary(g => g.Key, g => g.First());

        var rows = data.StudentMap.Values
            .OrderBy(s => s.RollNo)
            .Select(student =>
            {
                var row = new Dictionary<string, string?>
                {
                    { "RollNo", student.RollNo?.ToString() ?? "—" },
                    { "StudentName", BuildStudentName(student) }
                };

                double total = 0;
                foreach (var (subjectId, _) in orderedSubjects)
                {
                    markIndex.TryGetValue((student.Id, subjectId), out var mark);
                    var val = mark is null ? "—" : mark.IsAbsent ? "Ab" : (mark.MarksObtained ?? 0).ToString("F2");
                    if (mark is { IsAbsent: false })
                        total += mark.MarksObtained ?? 0;
                    row[$"subj_{subjectId}"] = val;
                }

                var pct = data.TotalMaxMarks > 0 ? Math.Round(total / data.TotalMaxMarks * 100, 2) : 0;
                row["Total"] = total.ToString("F2");
                row["Percentage"] = $"{pct}%";
                row["Grade"] = ComputeGrade(total, data.TotalMaxMarks);
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
        bool exportAllRows)
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
        var data = await LoadExamReportDataAsync(request.Filters.ExamId.Value, request.Filters);
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
        var passPct = total > 0 ? Math.Round((double) passed / total * 100, 2) : 0;

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
}
