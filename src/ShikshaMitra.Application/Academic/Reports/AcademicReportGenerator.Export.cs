using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    internal async Task<AcademicReportExportRows> GenerateExportAsync(AcademicReportRequestDto request,
        Guid userId, Guid? tenantId, CancellationToken cancellationToken = default, int batchSize = 256)
    {
        NormalizeAndValidateRequest(request, exportAllRows: true);
        if (batchSize < 1 || batchSize > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var columns = CurriculumExportColumns(request.ReportType);
        if (columns == null)
        {
            var examExport = await TryCreateExamExportAsync(request, userId, tenantId, cancellationToken, batchSize);
            if (examExport != null) return examExport;
            var timetableExport = await TryCreateTimetableExportAsync(request, cancellationToken, batchSize);
            if (timetableExport != null) return timetableExport;
            var learningExport = await TryCreateLearningTaskExportAsync(request, cancellationToken, batchSize);
            if (learningExport != null) return learningExport;
            var analyticsExport = await TryCreateLearningAnalyticsExportAsync(request, batchSize, cancellationToken);
            if (analyticsExport != null) return analyticsExport;
            throw new BusinessException("Academic:Reports:UnknownReportType").WithData("ReportType", request.ReportType);
        }
        var metadata = new AcademicReportPreviewDto
        {
            ReportType = request.ReportType, GeneratedAtUtc = DateTime.UtcNow,
            FilterSummary = BuildFilterSummary(request.Filters), NormalizedFilters = request.Filters,
            Columns = columns.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label }).ToList(),
            Page = 1
        };
        return new AcademicReportExportRows(metadata, ReadCurriculumExportRowsAsync(request, batchSize, cancellationToken));
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadCurriculumExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var source = BuildCurriculumExportRowsAsync(request, batchSize, cancellationToken);
        await foreach (var row in AcademicReportExternalSort.SortAsync(source,
            (left, right) => CompareCurriculumRows(left, right, request.ReportType), cancellationToken: cancellationToken))
            yield return row.Values;
    }

    private async IAsyncEnumerable<CurriculumExportRow> BuildCurriculumExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var isSummary = request.ReportType is AcademicReportTypes.SubjectWiseChapterCoverage or AcademicReportTypes.CurriculumCompletionReport;
        var isPrimaryTeacher = request.ReportType == AcademicReportTypes.SubjectPrimaryTeacherReport;
        CurriculumExportItem? summarySubject = null;
        var total = 0;
        var completed = 0;
        await foreach (var item in ReadCurriculumExportItemsAsync(request, batchSize, cancellationToken))
        {
            if (isSummary)
            {
                if (summarySubject != null && summarySubject.AssignmentId != item.AssignmentId)
                {
                    yield return BuildCurriculumSummary(summarySubject, total, completed, request.ReportType);
                    total = 0;
                    completed = 0;
                }
                summarySubject = item;
                if (item.ChapterId.HasValue)
                {
                    total++;
                    if (item.Progress == 100) completed++;
                }
                continue;
            }
            if (!isPrimaryTeacher && !item.ChapterId.HasValue) continue;
            if (request.ReportType is AcademicReportTypes.ChapterProgressReport or AcademicReportTypes.ChapterDescriptionSummary or AcademicReportTypes.ChapterRevisionTracker &&
                request.Filters.ChapterIds is { Count: > 0 } && !request.Filters.ChapterIds.Contains(item.ChapterId!.Value)) continue;
            var values = new Dictionary<string, string?> { ["SubjectName"] = item.SubjectName ?? "—" };
            if (!isPrimaryTeacher) values["ChapterName"] = item.ChapterName;
            switch (request.ReportType)
            {
                case AcademicReportTypes.SubjectPrimaryTeacherReport:
                    values["PrimaryTeacher"] = item.TeacherName;
                    break;
                case AcademicReportTypes.ChapterProgressReport:
                    values["Progress"] = $"{item.Progress}%";
                    values["TeacherName"] = item.TeacherName;
                    break;
                case AcademicReportTypes.ChapterDescriptionSummary:
                    values["ChapterNumber"] = item.ChapterNumber.ToString();
                    values["Description"] = Regex.Replace(item.Description ?? string.Empty, "<.*?>", string.Empty);
                    values["LearningObjectives"] = string.Empty;
                    values["TotalTopics"] = string.Empty;
                    break;
                case AcademicReportTypes.TimelineReport:
                    values["PlannedStartDate"] = item.PlannedStart.ToString("dd-MM-yyyy");
                    values["PlannedEndDate"] = item.PlannedEnd.ToString("dd-MM-yyyy");
                    values["ActualStartDate"] = item.StartDate?.ToString("dd-MM-yyyy") ?? string.Empty;
                    values["ActualEndDate"] = item.CompletionDate?.ToString("dd-MM-yyyy") ?? string.Empty;
                    values["DelayDays"] = item.CompletionDate.HasValue && item.CompletionDate.Value > item.PlannedEnd.DateTime
                        ? ((int)(item.CompletionDate.Value - item.PlannedEnd.DateTime).TotalDays).ToString() : string.Empty;
                    values["Status"] = CurriculumProgressStatus(item.Progress);
                    break;
                case AcademicReportTypes.ClassWiseChapterAssignment:
                    values["ClassName"] = item.StandardName ?? "—";
                    values["AssignedTeacher"] = item.TeacherName;
                    values["AssignmentDate"] = item.CreationTime.ToString("dd-MM-yyyy");
                    values["Status"] = CurriculumProgressStatus(item.Progress);
                    break;
                case AcademicReportTypes.ChapterRevisionTracker:
                    values["FirstCompletionDate"] = item.CompletionDate?.ToString("dd-MM-yyyy") ?? string.Empty;
                    values["RevisionCount"] = string.Empty;
                    values["TeacherName"] = item.TeacherName;
                    break;
            }
            yield return new CurriculumExportRow(item, values);
        }
        if (summarySubject != null)
            yield return BuildCurriculumSummary(summarySubject, total, completed, request.ReportType);
    }

    private async IAsyncEnumerable<CurriculumExportItem> ReadCurriculumExportItemsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var assignments = await assignedSubjectRepository.GetQueryableAsync();
        if (request.Filters.SubjectIds is { Count: > 0 }) assignments = assignments.Where(s => request.Filters.SubjectIds.Contains(s.SubjectId));
        if (request.Filters.ClassIds is { Count: > 0 }) assignments = assignments.Where(s => request.Filters.ClassIds.Contains(s.StandardId));
        var primaryOnly = request.ReportType == AcademicReportTypes.SubjectPrimaryTeacherReport;
        var subjects = assignments.Select(s => new
        {
            s.Id, SubjectName = s.Subject.Name, StandardName = s.Standard.Name,
            TeacherId = s.Teachers.Where(t => t.TeacherType == SubjectTeacherType.Primary)
                .OrderBy(t => t.TeacherId).Select(t => (Guid?)t.TeacherId).FirstOrDefault()
                ?? (primaryOnly ? null : s.Teachers.OrderBy(t => t.TeacherId).Select(t => (Guid?)t.TeacherId).FirstOrDefault()),
            s.Chapters
        });
        var query = primaryOnly
            ? subjects.Select(s => new CurriculumExportItem
            {
                AssignmentId = s.Id, SubjectName = s.SubjectName, StandardName = s.StandardName, TeacherId = s.TeacherId
            })
            : subjects.SelectMany(s => s.Chapters.DefaultIfEmpty(), (s, c) => new CurriculumExportItem
            {
                AssignmentId = s.Id, SubjectName = s.SubjectName, StandardName = s.StandardName, TeacherId = s.TeacherId,
                ChapterId = c == null ? null : c.Id, ChapterName = c == null ? null : c.ChapterName,
                ChapterNumber = c == null ? 0 : c.ChapterNumber, Description = c == null ? null : c.Description,
                PlannedStart = c == null ? default : c.ChapterStartTime, PlannedEnd = c == null ? default : c.ChapterEndTime,
                CreationTime = c == null ? default : c.CreationTime
            });
        var ordered = query.OrderBy(item => item.AssignmentId);
        if (!primaryOnly) ordered = ordered.ThenBy(item => item.ChapterId);
        var needsTeachers = request.ReportType is AcademicReportTypes.SubjectPrimaryTeacherReport or AcademicReportTypes.ChapterProgressReport
            or AcademicReportTypes.ClassWiseChapterAssignment or AcademicReportTypes.ChapterRevisionTracker;
        for (var offset = 0; ; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = await asyncExecuter.ToListAsync(ordered.Skip(offset).Take(batchSize), cancellationToken);
            if (items.Count == 0) yield break;
            var chapterIds = items.Where(item => item.ChapterId.HasValue).Select(item => item.ChapterId!.Value).ToList();
            var progress = request.ReportType == AcademicReportTypes.ChapterDescriptionSummary
                ? new Dictionary<Guid, (int Progress, DateTime? StartDate, DateTime? CompletionDate)>()
                : await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);
            var teacherNames = new Dictionary<Guid, string>();
            var teacherIds = items.Where(item => item.TeacherId.HasValue).Select(item => item.TeacherId!.Value).Distinct().ToList();
            if (needsTeachers && teacherIds.Count > 0)
            {
                var employees = await employeeRepository.GetQueryableAsync();
                var names = await asyncExecuter.ToListAsync(employees.Where(e => teacherIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.FirstName, e.LastName }), cancellationToken);
                teacherNames = names.ToDictionary(e => e.Id,
                    e => string.Join(" ", new[] { e.FirstName, e.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))));
            }
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.ChapterId.HasValue && progress.TryGetValue(item.ChapterId.Value, out var p))
                {
                    item.Progress = p.Progress;
                    item.StartDate = p.StartDate;
                    item.CompletionDate = p.CompletionDate;
                }
                item.TeacherName = item.TeacherId.HasValue ? teacherNames.GetValueOrDefault(item.TeacherId.Value, "—") : "—";
                yield return item;
            }
            if (items.Count < batchSize) yield break;
        }
    }

    private static CurriculumExportRow BuildCurriculumSummary(CurriculumExportItem item, int total, int completed, string reportType)
    {
        var percentage = total == 0 ? 0 : Math.Round((double)completed / total * 100, 2);
        var values = new Dictionary<string, string?>
        {
            ["SubjectName"] = item.SubjectName ?? "—", ["TotalChapters"] = total.ToString(),
            ["RemainingChapters"] = (total - completed).ToString()
        };
        if (reportType == AcademicReportTypes.SubjectWiseChapterCoverage)
        {
            values["CoveredChapters"] = completed.ToString();
            values["CoveragePercent"] = $"{percentage}%";
        }
        else
        {
            values["ClassName"] = item.StandardName ?? "—";
            values["CompletedChapters"] = completed.ToString();
            values["CompletionPercent"] = $"{percentage}%";
        }
        return new CurriculumExportRow(item, values);
    }

    private static string CurriculumProgressStatus(int progress) => progress == 100 ? "Completed" : progress > 0 ? "In Progress" : "Upcoming";

    private static int CompareCurriculumRows(CurriculumExportRow left, CurriculumExportRow right, string reportType)
    {
        var a = left.Item;
        var b = right.Item;
        var compare = 0;
        if (reportType is AcademicReportTypes.ClassWiseChapterAssignment or AcademicReportTypes.CurriculumCompletionReport)
            compare = Comparer<string?>.Default.Compare(a.StandardName, b.StandardName);
        if (compare == 0) compare = Comparer<string?>.Default.Compare(a.SubjectName, b.SubjectName);
        if (compare == 0 && reportType == AcademicReportTypes.SubjectPrimaryTeacherReport)
            compare = Comparer<string?>.Default.Compare(a.StandardName, b.StandardName);
        if (compare == 0) compare = a.AssignmentId.CompareTo(b.AssignmentId);
        if (compare == 0) compare = reportType == AcademicReportTypes.TimelineReport
            ? a.PlannedStart.CompareTo(b.PlannedStart) : a.ChapterNumber.CompareTo(b.ChapterNumber);
        return compare;
    }

    private static (string Id, string Label)[]? CurriculumExportColumns(string reportType) => reportType switch
    {
        AcademicReportTypes.ChapterProgressReport => [("SubjectName", "Subject Name"), ("ChapterName", "Chapter Name"), ("Progress", "Progress (%)"), ("TeacherName", "Teacher Name")],
        AcademicReportTypes.SubjectPrimaryTeacherReport => [("SubjectName", "Subject Name"), ("PrimaryTeacher", "Primary Teacher")],
        AcademicReportTypes.SubjectWiseChapterCoverage => [("SubjectName", "Subject"), ("TotalChapters", "Total Chapters"), ("CoveredChapters", "Covered Chapters"), ("RemainingChapters", "Remaining Chapters"), ("CoveragePercent", "Coverage %")],
        AcademicReportTypes.ChapterDescriptionSummary => [("SubjectName", "Subject"), ("ChapterNumber", "Chapter Number"), ("ChapterName", "Chapter Name"), ("Description", "Chapter Description"), ("LearningObjectives", "Learning Objectives"), ("TotalTopics", "Total Topics")],
        AcademicReportTypes.TimelineReport => [("SubjectName", "Subject"), ("ChapterName", "Chapter"), ("PlannedStartDate", "Planned Start Date"), ("PlannedEndDate", "Planned End Date"), ("ActualStartDate", "Actual Start Date"), ("ActualEndDate", "Actual End Date"), ("DelayDays", "Delay Days"), ("Status", "Status")],
        AcademicReportTypes.ClassWiseChapterAssignment => [("ClassName", "Class"), ("SubjectName", "Subject"), ("ChapterName", "Chapter"), ("AssignedTeacher", "Assigned Teacher"), ("AssignmentDate", "Assignment Date"), ("Status", "Status")],
        AcademicReportTypes.CurriculumCompletionReport => [("ClassName", "Class"), ("SubjectName", "Subject"), ("TotalChapters", "Total Chapters"), ("CompletedChapters", "Completed Chapters"), ("CompletionPercent", "Completion %"), ("RemainingChapters", "Remaining Chapters")],
        AcademicReportTypes.ChapterRevisionTracker => [("SubjectName", "Subject"), ("ChapterName", "Chapter"), ("FirstCompletionDate", "First Completion Date"), ("RevisionCount", "Revision Count"), ("TeacherName", "Teacher")],
        _ => null
    };

    private sealed class CurriculumExportItem
    {
        public Guid AssignmentId { get; set; }
        public string? SubjectName { get; set; }
        public string? StandardName { get; set; }
        public Guid? TeacherId { get; set; }
        public string TeacherName { get; set; } = "—";
        public Guid? ChapterId { get; set; }
        public string? ChapterName { get; set; }
        public int ChapterNumber { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset PlannedStart { get; set; }
        public DateTimeOffset PlannedEnd { get; set; }
        public DateTime CreationTime { get; set; }
        public int Progress { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? CompletionDate { get; set; }
    }

    private sealed record CurriculumExportRow(CurriculumExportItem Item, Dictionary<string, string?> Values);
}
