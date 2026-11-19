using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto?> TryBuildCurriculumPreviewAsync(
        AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var columns = CurriculumExportColumns(request.ReportType);
        if (columns == null) return null;
        payload.Columns = columns.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label }).ToList();
        var assignments = await assignedSubjectRepository.GetQueryableAsync();
        if (request.Filters.SubjectIds is { Count: > 0 }) assignments = assignments.Where(s => request.Filters.SubjectIds.Contains(s.SubjectId));
        if (request.Filters.ClassIds is { Count: > 0 }) assignments = assignments.Where(s => request.Filters.ClassIds.Contains(s.StandardId));
        var summary = request.ReportType is AcademicReportTypes.SubjectWiseChapterCoverage or AcademicReportTypes.CurriculumCompletionReport;
        var primaryTeacher = request.ReportType == AcademicReportTypes.SubjectPrimaryTeacherReport;
        var filterChapters = request.ReportType is AcademicReportTypes.ChapterProgressReport or AcademicReportTypes.ChapterDescriptionSummary
            or AcademicReportTypes.ChapterRevisionTracker && request.Filters.ChapterIds is { Count: > 0 };
        var selectedChapters = request.Filters.ChapterIds ?? new List<Guid>();

        // Retain .NET name collation and the parent/child sort hierarchy without loading child content.
        var keys = await asyncExecuter.ToListAsync(assignments.OrderBy(s => s.Id).Select(s => new CurriculumPreviewKey
        {
            Id = s.Id, SubjectName = s.Subject.Name, StandardName = s.Standard.Name,
            Count = summary || primaryTeacher ? 1 : s.Chapters.Count(c => !filterChapters || selectedChapters.Contains(c.Id))
        }));
        if (keys.Count == 0 && request.ReportType == AcademicReportTypes.ChapterProgressReport) return payload;
        var ordered = request.ReportType is AcademicReportTypes.ClassWiseChapterAssignment or AcademicReportTypes.CurriculumCompletionReport
            ? keys.OrderBy(key => key.StandardName).ThenBy(key => key.SubjectName)
            : primaryTeacher ? keys.OrderBy(key => key.SubjectName).ThenBy(key => key.StandardName)
            : keys.OrderBy(key => key.SubjectName).ThenBy(key => key.Id);
        payload.TotalCount = keys.Sum(key => key.Count);
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
        var skip = Math.Max(0, unchecked((request.Page - 1) * request.PageSize));
        var remaining = request.PageSize;
        var slices = new List<(CurriculumPreviewKey Key, int Skip, int Take)>();
        foreach (var key in ordered)
        {
            if (skip >= key.Count) { skip -= key.Count; continue; }
            var take = Math.Min(remaining, key.Count - skip);
            slices.Add((key, skip, take));
            remaining -= take;
            skip = 0;
            if (remaining == 0) break;
        }
        if (slices.Count == 0) return payload;
        if (summary) return await BuildCurriculumSummaryPreviewAsync(assignments, request, payload, slices.Select(slice => slice.Key).ToList());

        var selectedAssignmentIds = slices.Select(slice => slice.Key.Id).ToList();
        var roots = assignments.Where(s => selectedAssignmentIds.Contains(s.Id));
        List<CurriculumExportItem> items;
        if (primaryTeacher)
        {
            items = await asyncExecuter.ToListAsync(roots.Select(s => new CurriculumExportItem
            {
                AssignmentId = s.Id, SubjectName = s.Subject.Name, StandardName = s.Standard.Name,
                TeacherId = s.Teachers.Where(t => t.TeacherType == SubjectTeacherType.Primary)
                    .OrderBy(t => t.TeacherId).Select(t => (Guid?)t.TeacherId).FirstOrDefault()
            }).Take(request.PageSize));
            items = items.OrderBy(item => selectedAssignmentIds.IndexOf(item.AssignmentId)).ToList();
        }
        else
        {
            var children = roots.SelectMany(s => s.Chapters, (s, c) => new CurriculumExportItem
            {
                AssignmentId = s.Id, SubjectName = s.Subject.Name, StandardName = s.Standard.Name,
                TeacherId = s.Teachers.Where(t => t.TeacherType == SubjectTeacherType.Primary)
                    .OrderBy(t => t.TeacherId).Select(t => (Guid?)t.TeacherId).FirstOrDefault()
                    ?? s.Teachers.OrderBy(t => t.TeacherId).Select(t => (Guid?)t.TeacherId).FirstOrDefault(),
                ChapterId = c.Id, ChapterName = c.ChapterName, ChapterNumber = c.ChapterNumber,
                Description = c.Description, PlannedStart = c.ChapterStartTime, PlannedEnd = c.ChapterEndTime, CreationTime = c.CreationTime
            });
            if (filterChapters) children = children.Where(c => request.Filters.ChapterIds!.Contains(c.ChapterId!.Value));
            if (request.ReportType == AcademicReportTypes.TimelineReport)
            {
                // DateTimeOffset comparison stays in .NET, including providers without SQL offset ordering.
                var dates = await asyncExecuter.ToListAsync(children.OrderBy(c => c.ChapterId)
                    .Select(c => new { c.AssignmentId, c.ChapterId, c.PlannedStart }));
                var ids = slices.SelectMany(slice => dates.Where(c => c.AssignmentId == slice.Key.Id)
                    .OrderBy(c => c.PlannedStart).Skip(slice.Skip).Take(slice.Take).Select(c => c.ChapterId!.Value)).ToList();
                items = await asyncExecuter.ToListAsync(children.Where(c => ids.Contains(c.ChapterId!.Value)).Take(request.PageSize));
                items = items.OrderBy(c => ids.IndexOf(c.ChapterId!.Value)).ToList();
            }
            else
            {
                var fullIds = slices.Where(slice => slice.Skip == 0 && slice.Take == slice.Key.Count).Select(slice => slice.Key.Id).ToList();
                IQueryable<CurriculumExportItem>? page = fullIds.Count == 0 ? null
                    : children.Where(c => fullIds.Contains(c.AssignmentId)).Take(request.PageSize);
                foreach (var slice in slices.Where(slice => slice.Skip != 0 || slice.Take != slice.Key.Count))
                {
                    var part = children.Where(c => c.AssignmentId == slice.Key.Id).OrderBy(c => c.ChapterNumber).ThenBy(c => c.ChapterId)
                        .Skip(slice.Skip).Take(slice.Take);
                    page = page == null ? part : page.Concat(part);
                }
                items = await asyncExecuter.ToListAsync(page!);
                items = items.OrderBy(item => selectedAssignmentIds.IndexOf(item.AssignmentId))
                    .ThenBy(item => item.ChapterNumber).ThenBy(item => item.ChapterId).ToList();
            }
        }
        var chapterIds = items.Where(item => item.ChapterId.HasValue).Select(item => item.ChapterId!.Value).ToList();
        var progress = request.ReportType == AcademicReportTypes.ChapterDescriptionSummary || primaryTeacher
            ? new Dictionary<Guid, (int Progress, DateTime? StartDate, DateTime? CompletionDate)>()
            : await LoadChapterProgressAsync(chapterIds, request.Filters.DivisionIds);
        var teacherIds = items.Where(item => item.TeacherId.HasValue).Select(item => item.TeacherId!.Value).Distinct().ToList();
        var teachers = new Dictionary<Guid, string>();
        var needsTeachers = request.ReportType is AcademicReportTypes.SubjectPrimaryTeacherReport or AcademicReportTypes.ChapterProgressReport
            or AcademicReportTypes.ClassWiseChapterAssignment or AcademicReportTypes.ChapterRevisionTracker;
        if (needsTeachers && teacherIds.Count > 0)
        {
            var employees = await employeeRepository.GetQueryableAsync();
            var names = await asyncExecuter.ToListAsync(employees.Where(employee => teacherIds.Contains(employee.Id))
                .Select(employee => new { employee.Id, employee.FirstName, employee.LastName }));
            teachers = names.ToDictionary(employee => employee.Id,
                employee => string.Join(" ", new[] { employee.FirstName, employee.LastName }.Where(name => !string.IsNullOrWhiteSpace(name))));
        }
        foreach (var item in items)
        {
            if (item.ChapterId.HasValue && progress.TryGetValue(item.ChapterId.Value, out var value))
            {
                item.Progress = value.Progress; item.StartDate = value.StartDate; item.CompletionDate = value.CompletionDate;
            }
            item.TeacherName = item.TeacherId.HasValue ? teachers.GetValueOrDefault(item.TeacherId.Value, "—") : "—";
            payload.Rows.Add(FormatCurriculumPreviewItem(item, request.ReportType));
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildCurriculumSummaryPreviewAsync(IQueryable<AssignedSubject> assignments,
        AcademicReportRequestDto request, AcademicReportPreviewDto payload, List<CurriculumPreviewKey> keys)
    {
        var progress = await chapterProgressRepository.GetQueryableAsync();
        if (request.Filters.DivisionIds is { Count: > 0 }) progress = progress.Where(p => request.Filters.DivisionIds.Contains(p.DivisionId));
        var averages = progress.GroupBy(p => p.ChapterId).Select(group => new { ChapterId = group.Key, Average = group.Average(p => p.Progress) });
        var ids = keys.Select(key => key.Id).ToList();
        var summaries = await asyncExecuter.ToListAsync(assignments.Where(s => ids.Contains(s.Id)).Select(s => new
        {
            s.Id, Total = s.Chapters.Count,
            // These inclusive bounds are exactly the values whose midpoint-to-even rounded average is 100.
            Completed = s.Chapters.Count(c => averages.Any(p => p.ChapterId == c.Id && p.Average >= 99.5 && p.Average <= 100.5))
        }).Take(request.PageSize));
        var counts = summaries.ToDictionary(summary => summary.Id);
        foreach (var key in keys)
        {
            var count = counts[key.Id];
            payload.Rows.Add(BuildCurriculumSummary(new CurriculumExportItem
            { SubjectName = key.SubjectName, StandardName = key.StandardName }, count.Total, count.Completed, request.ReportType).Values);
        }
        return payload;
    }

    private static Dictionary<string, string?> FormatCurriculumPreviewItem(CurriculumExportItem item, string reportType)
    {
        var values = new Dictionary<string, string?> { ["SubjectName"] = item.SubjectName ?? "—" };
        if (reportType != AcademicReportTypes.SubjectPrimaryTeacherReport) values["ChapterName"] = item.ChapterName;
        switch (reportType)
        {
            case AcademicReportTypes.SubjectPrimaryTeacherReport:
                values["PrimaryTeacher"] = item.TeacherName; break;
            case AcademicReportTypes.ChapterProgressReport:
                values["Progress"] = $"{item.Progress}%"; values["TeacherName"] = item.TeacherName; break;
            case AcademicReportTypes.ChapterDescriptionSummary:
                values["ChapterNumber"] = item.ChapterNumber.ToString();
                values["Description"] = System.Text.RegularExpressions.Regex.Replace(item.Description ?? string.Empty, "<.*?>", string.Empty);
                values["LearningObjectives"] = string.Empty; values["TotalTopics"] = string.Empty; break;
            case AcademicReportTypes.TimelineReport:
                values["PlannedStartDate"] = item.PlannedStart.ToString("dd-MM-yyyy");
                values["PlannedEndDate"] = item.PlannedEnd.ToString("dd-MM-yyyy");
                values["ActualStartDate"] = item.StartDate?.ToString("dd-MM-yyyy") ?? string.Empty;
                values["ActualEndDate"] = item.CompletionDate?.ToString("dd-MM-yyyy") ?? string.Empty;
                values["DelayDays"] = item.CompletionDate.HasValue && item.CompletionDate.Value > item.PlannedEnd.DateTime
                    ? ((int)(item.CompletionDate.Value - item.PlannedEnd.DateTime).TotalDays).ToString() : string.Empty;
                values["Status"] = CurriculumProgressStatus(item.Progress); break;
            case AcademicReportTypes.ClassWiseChapterAssignment:
                values["ClassName"] = item.StandardName ?? "—"; values["AssignedTeacher"] = item.TeacherName;
                values["AssignmentDate"] = item.CreationTime.ToString("dd-MM-yyyy"); values["Status"] = CurriculumProgressStatus(item.Progress); break;
            case AcademicReportTypes.ChapterRevisionTracker:
                values["FirstCompletionDate"] = item.CompletionDate?.ToString("dd-MM-yyyy") ?? string.Empty;
                values["RevisionCount"] = string.Empty; values["TeacherName"] = item.TeacherName; break;
        }
        return values;
    }

    private sealed class CurriculumPreviewKey
    {
        public Guid Id { get; init; }
        public string? SubjectName { get; init; }
        public string? StandardName { get; init; }
        public int Count { get; init; }
    }
}
