using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private Task<AcademicReportPreviewDto> BuildLearningAnalyticsPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload) => request.ReportType switch
    {
        AcademicReportTypes.StudentPerformanceTrendReport => BuildLearningTrendPreviewAsync(request, payload),
        AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport => BuildLearningTeacherPreviewAsync(request, payload),
        AcademicReportTypes.ClasswiseOverallHomeworkAssignmentReport => BuildLearningOverallPreviewAsync(request, payload),
        _ => BuildLearningLoadPreviewAsync(request, payload)
    };

    private async Task<AcademicReportPreviewDto> BuildLearningLoadPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload)
    {
        var assignments = request.ReportType == AcademicReportTypes.AssignmentLoadAnalysisReport;
        var roots = await LoadLearningPreviewTaskKeysAsync(await GetLearningExportTasksAsync(request.Filters, assignments));
        if (roots.Count == 0) return payload;
        var span = request.Filters.FromDate.HasValue && request.Filters.ToDate.HasValue
            ? request.Filters.ToDate.Value - request.Filters.FromDate.Value : roots.Max(task => task.CreationTime) - roots.Min(task => task.CreationTime);
        var periods = Math.Max(1, span.TotalDays / (assignments ? 30.44 : 7));
        var schedules = await LoadLearningPreviewScheduleKeysAsync(request.Filters, assignments);
        var groups = schedules.GroupBy(source => (source.Task.StandardId, source.DivisionId, source.Task.SubjectId))
            .Select(group => new LearningPreviewGroup(group.First(), group.Select(source => source.Task).DistinctBy(task => task.Id).ToList(), group.ToList())).ToList();
        await EnrichLearningExportAsync(groups.Select(group => group.Source).ToList(), CancellationToken.None);
        SetLearningPreviewPage(request, payload, groups.Count);
        var page = groups.OrderBy(group => group.Source.Task.StandardSortName).ThenBy(group => group.Source.DivisionSortName)
            .ThenBy(group => group.Source.Task.SubjectSortName).Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        if (page.Count == 0) return payload;
        var stats = (await ReadLearningPreviewStatsAsync(request.Filters, assignments,
            page.SelectMany(group => group.Tasks).Select(task => task.Id).Distinct().ToArray(),
            page.Select(group => group.Source.DivisionId).Distinct().ToArray(), byDivision: true)).ToDictionary(stat => (stat.TaskId, stat.DivisionId));
        foreach (var group in page)
            payload.Rows.Add(BuildLearningLoadRow(CreateLearningPreviewAccumulator(group.Schedules!, stats)!, periods, assignments));
        return payload;
    }

    private static LearningLoadAccumulator? CreateLearningPreviewAccumulator(IEnumerable<LearningExportSubmission> sources,
        IReadOnlyDictionary<(Guid TaskId, Guid DivisionId), LearningPreviewStats> stats)
    {
        LearningLoadAccumulator? result = null;
        foreach (var source in sources.DistinctBy(source => source.Task.Id).OrderBy(source => source.Task.Id))
        {
            var count = stats.GetValueOrDefault((source.Task.Id, source.DivisionId));
            var stat = new LearningScheduleStat(source, count?.Count ?? 0, count?.Submitted ?? 0, 0,
                count?.Marked ?? 0, count?.ScoreSum ?? 0);
            result ??= new LearningLoadAccumulator(stat);
            result.Add(stat);
        }
        return result;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningOverallPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload)
    {
        var homework = await LoadLearningPreviewTaskKeysAsync(await GetLearningExportTasksAsync(request.Filters, false));
        var assignments = await LoadLearningPreviewTaskKeysAsync(await GetLearningExportTasksAsync(request.Filters, true));
        if (homework.Count == 0 && assignments.Count == 0) return payload;
        var hwSchedules = await LoadLearningPreviewScheduleKeysAsync(request.Filters, false);
        var aSchedules = await LoadLearningPreviewScheduleKeysAsync(request.Filters, true);
        var sources = hwSchedules.Select(source => new LearningPreviewFamilySource(false, source))
            .Concat(aSchedules.Select(source => new LearningPreviewFamilySource(true, source)));
        var groups = sources.GroupBy(fact => (fact.Source.Task.StandardId, fact.Source.DivisionId))
            .Select(group => new { Source = group.First().Source, Facts = group.ToList() }).ToList();
        await EnrichLearningExportAsync(groups.Select(group => group.Source).ToList(), CancellationToken.None);
        SetLearningPreviewPage(request, payload, groups.Count);
        var page = groups.OrderBy(group => group.Source.Task.StandardSortName).ThenBy(group => group.Source.DivisionSortName)
            .Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        if (page.Count == 0) return payload;
        var divisionIds = page.Select(group => group.Source.DivisionId).Distinct().ToArray();
        var homeworkStats = (await ReadLearningPreviewStatsAsync(request.Filters, false,
            page.SelectMany(group => group.Facts).Where(fact => !fact.Assignment).Select(fact => fact.Source.Task.Id).Distinct().ToArray(),
            divisionIds, byDivision: true)).ToDictionary(stat => (stat.TaskId, stat.DivisionId));
        var assignmentStats = (await ReadLearningPreviewStatsAsync(request.Filters, true,
            page.SelectMany(group => group.Facts).Where(fact => fact.Assignment).Select(fact => fact.Source.Task.Id).Distinct().ToArray(),
            divisionIds, byDivision: true)).ToDictionary(stat => (stat.TaskId, stat.DivisionId));
        foreach (var group in page)
            payload.Rows.Add(BuildLearningOverallRow(group.Source,
                CreateLearningPreviewAccumulator(group.Facts.Where(fact => !fact.Assignment).Select(fact => fact.Source), homeworkStats),
                CreateLearningPreviewAccumulator(group.Facts.Where(fact => fact.Assignment).Select(fact => fact.Source), assignmentStats)));
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningTeacherPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload)
    {
        var roots = await LoadLearningPreviewTaskKeysAsync(await GetLearningExportTasksAsync(request.Filters, false));
        if (roots.Count == 0) return payload;
        var groups = roots.Where(task => task.CreatorId.HasValue).GroupBy(task => task.CreatorId!.Value)
            .Select(group => new LearningPreviewGroup(new() { Task = group.First() }, group.ToList())).ToList();
        await EnrichLearningExportAsync(groups.Select(group => group.Source).ToList(), CancellationToken.None);
        SetLearningPreviewPage(request, payload, groups.Count);
        var page = groups.OrderBy(group => group.Source.Task.TeacherSortName).Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        if (page.Count == 0) return payload;
        var ids = page.SelectMany(group => group.Tasks).Select(task => task.Id).ToArray();
        var stats = (await ReadLearningPreviewStatsAsync(request.Filters, false, ids)).ToDictionary(stat => stat.TaskId);
        var source = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, false);
        var times = await asyncExecuter.ToListAsync(source.Where(submission => submission.HomeworkAssignmentId.HasValue &&
                Enumerable.Contains(ids, submission.HomeworkAssignmentId.Value) && submission.Status == SubmissionStatus.Reviewed &&
                submission.ReviewedBy.HasValue && submission.ReviewedAt.HasValue && submission.SubmissionDate.HasValue)
            .Select(submission => new { TaskId = submission.HomeworkAssignmentId!.Value, submission.ReviewedAt, submission.SubmissionDate }));
        var timesByTask = times.ToLookup(row => row.TaskId);
        foreach (var group in page)
        {
            var evaluated = group.Tasks.Sum(task => stats.GetValueOrDefault(task.Id)?.EvaluatedWithReviewer ?? 0);
            var pending = group.Tasks.Sum(task => stats.GetValueOrDefault(task.Id)?.PendingEvaluation ?? 0);
            var hours = group.Tasks.SelectMany(task => timesByTask[task.Id])
                .Select(row => (row.ReviewedAt!.Value - row.SubmissionDate!.Value).TotalHours).ToList();
            var average = hours.Count == 0 ? 0 : Math.Round(hours.Average(), 1);
            payload.Rows.Add(new()
            {
                ["Teacher"] = group.Source.Task.TeacherName, ["HomeworkAssigned"] = group.Tasks.Count.ToString(),
                ["HomeworkEvaluated"] = evaluated.ToString(), ["PendingEvaluation"] = pending.ToString(),
                ["AvgEvaluationTime"] = hours.Count == 0 ? "—" : average < 24 ? $"{average} hrs" : $"{Math.Round(average / 24, 1)} days",
                ["EvaluationCompletionPercent"] = LearningPercent(evaluated, evaluated + pending)
            });
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningTrendPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload)
    {
        if (request.Filters.StudentIds is not { Count: > 0 }) return payload;
        var homework = await LoadLearningPreviewTaskKeysAsync(await GetLearningExportTasksAsync(request.Filters, false));
        var assignments = await LoadLearningPreviewTaskKeysAsync(await GetLearningExportTasksAsync(request.Filters, true));
        var homeworkById = homework.ToDictionary(task => task.Id);
        var assignmentsById = assignments.ToDictionary(task => task.Id);
        var hwEntities = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, false);
        var aEntities = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, true);
        var hwEncounter = await asyncExecuter.ToListAsync(hwEntities.Select(submission => submission.HomeworkAssignmentId!.Value));
        var aEncounter = await asyncExecuter.ToListAsync(aEntities.Select(submission => submission.AssignmentId!.Value));
        var subjects = hwEncounter.Select(id => homeworkById[id].SubjectId).Concat(aEncounter.Select(id => assignmentsById[id].SubjectId)).Distinct().ToList();
        var subjectTasks = homework.Concat(assignments).GroupBy(task => task.SubjectId).ToDictionary(group => group.Key, group => group.First());
        await EnrichLearningExportAsync(subjectTasks.Values.Select(task => new LearningExportSubmission { Task = task }).ToList(), CancellationToken.None);
        var studentSources = request.Filters.StudentIds.Distinct().Select(id => new LearningExportSubmission { StudentId = id }).ToList();
        await EnrichLearningExportAsync(studentSources, CancellationToken.None);
        var students = studentSources.Where(source => source.StudentName != null).ToDictionary(source => source.StudentId, source => source.StudentName);
        var hwStats = (await ReadLearningPreviewStudentStatsAsync(request.Filters, false)).ToDictionary(stat => (stat.StudentId, stat.SubjectId));
        var aStats = (await ReadLearningPreviewStudentStatsAsync(request.Filters, true)).ToDictionary(stat => (stat.StudentId, stat.SubjectId));
        var groups = new List<LearningPreviewGroup>();
        foreach (var studentId in request.Filters.StudentIds)
        {
            if (!students.TryGetValue(studentId, out var name)) continue;
            foreach (var subjectId in subjects)
            {
                var hw = hwStats.GetValueOrDefault((studentId, subjectId));
                var assignment = aStats.GetValueOrDefault((studentId, subjectId));
                if ((hw?.Count ?? 0) == 0 && (assignment?.Marked ?? 0) == 0) continue;
                groups.Add(new(new() { StudentId = studentId, StudentName = name, Task = subjectTasks[subjectId], SourceOrdinal = groups.Count }, [subjectTasks[subjectId]]));
            }
        }
        SetLearningPreviewPage(request, payload, groups.Count);
        var page = groups.Skip(PreviewSkip(request)).Take(request.PageSize).ToList();
        var periods = await ReadLearningPreviewPeriodsAsync(request, page, assignmentsById, aEntities);
        foreach (var group in page)
        {
            var source = group.Source;
            var key = (source.StudentId, source.Task.SubjectId);
            var hw = hwStats.GetValueOrDefault(key);
            var assignment = aStats.GetValueOrDefault(key);
            var period = periods.GetValueOrDefault(key);
            payload.Rows.Add(BuildLearningTrendRow(new(new LearningTrendFact(source, false, 0, 0, 0))
            {
                Homework = hw?.Count ?? 0, Submitted = hw?.Submitted ?? 0,
                Marked = assignment?.Marked ?? 0, Score = assignment?.ScoreSum ?? 0,
                Previous = period.Previous, PreviousScore = period.PreviousScore,
                Current = period.Current, CurrentScore = period.CurrentScore
            }));
        }
        return payload;
    }

    private async Task<List<LearningPreviewStudentStats>> ReadLearningPreviewStudentStatsAsync(AcademicReportFilterDto filters, bool assignments)
    {
        var source = await GetLearningExportSubmissionsAsync(filters, assignments);
        return await asyncExecuter.ToListAsync(source.GroupBy(submission => new { submission.StudentId, submission.Task.SubjectId })
            .Select(group => new LearningPreviewStudentStats
            {
                StudentId = group.Key.StudentId, SubjectId = group.Key.SubjectId, Count = group.Count(),
                Submitted = group.Count(submission => submission.Status == SubmissionStatus.Submitted || submission.Status == SubmissionStatus.Late ||
                    submission.Status == SubmissionStatus.Reviewed || submission.Status == SubmissionStatus.Excused || submission.Status == SubmissionStatus.NeedsReview),
                Marked = group.Count(submission => submission.Marks.HasValue),
                ScoreSum = group.Sum(submission => submission.Marks.HasValue && (submission.TotalMarks ?? submission.Task.TotalMarks) > 0
                    ? (double)submission.Marks!.Value / (double)(submission.TotalMarks ?? submission.Task.TotalMarks) * 100 : 0)
            }));
    }

    private async Task<Dictionary<(Guid StudentId, Guid SubjectId), LearningPreviewPeriod>> ReadLearningPreviewPeriodsAsync(
        AcademicReportRequestDto request, List<LearningPreviewGroup> page, Dictionary<Guid, LearningExportTask> tasks,
        IQueryable<StudentSubmissionEntity> source)
    {
        var result = new Dictionary<(Guid StudentId, Guid SubjectId), LearningPreviewPeriod>();
        if (page.Count == 0 || !request.Filters.FromDate.HasValue || !request.Filters.ToDate.HasValue) return result;
        var midpoint = request.Filters.FromDate.Value + (request.Filters.ToDate.Value - request.Filters.FromDate.Value) / 2;
        var students = page.Select(group => group.Source.StudentId).Distinct().ToArray();
        var subjects = page.Select(group => group.Source.Task.SubjectId).ToHashSet();
        var taskIds = tasks.Values.Where(task => subjects.Contains(task.SubjectId)).Select(task => task.Id).ToArray();
        var values = await asyncExecuter.ToListAsync(source.Where(submission => submission.Marks.HasValue && submission.SubmissionDate.HasValue &&
                Enumerable.Contains(students, submission.StudentId) && submission.AssignmentId.HasValue && Enumerable.Contains(taskIds, submission.AssignmentId.Value))
            .Select(submission => new { TaskId = submission.AssignmentId!.Value, submission.StudentId, submission.SubmissionDate, submission.Marks, submission.TotalMarks }));
        foreach (var value in values)
        {
            var task = tasks[value.TaskId];
            var key = (value.StudentId, task.SubjectId);
            var period = result.GetValueOrDefault(key);
            var total = (double)(value.TotalMarks ?? task.TotalMarks);
            var score = total > 0 ? (double)value.Marks!.Value / total * 100 : 0;
            if (value.SubmissionDate!.Value.UtcDateTime < midpoint) { period.Previous++; period.PreviousScore += score; }
            else { period.Current++; period.CurrentScore += score; }
            result[key] = period;
        }
        return result;
    }

    private sealed record LearningPreviewFamilySource(bool Assignment, LearningExportSubmission Source);
    private struct LearningPreviewPeriod { public int Previous, Current; public double PreviousScore, CurrentScore; }
    private sealed class LearningPreviewStudentStats
    {
        public Guid StudentId { get; init; }
        public Guid SubjectId { get; init; }
        public int Count { get; init; }
        public int Submitted { get; init; }
        public int Marked { get; init; }
        public double ScoreSum { get; init; }
    }
}
