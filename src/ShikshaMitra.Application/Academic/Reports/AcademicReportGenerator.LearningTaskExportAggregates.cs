using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async IAsyncEnumerable<List<LearningExportSubmission>> ReadLearningSubmissionBatchesAsync(
        AcademicReportFilterDto filters, bool assignments, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = await GetLearningExportTasksAsync(filters, assignments);
        var ids = tasks.Select(t => t.Id);
        var submissions = await submissionRepository.GetQueryableAsync();
        submissions = assignments
            ? submissions.Where(s => s.AssignmentId.HasValue && ids.Contains(s.AssignmentId.Value))
            : submissions.Where(s => s.HomeworkAssignmentId.HasValue && ids.Contains(s.HomeworkAssignmentId.Value));
        if (filters.DivisionIds is { Count: > 0 }) submissions = submissions.Where(s => filters.DivisionIds.Contains(s.DivisionId));
        if (filters.StudentIds is { Count: > 0 }) submissions = submissions.Where(s => filters.StudentIds.Contains(s.StudentId));
        var query = submissions.Select(s => new LearningExportSubmission
        {
            Task = new LearningExportTask { Id = assignments ? s.AssignmentId!.Value : s.HomeworkAssignmentId!.Value },
            Id = s.Id, StudentId = s.StudentId, DivisionId = s.DivisionId, DueDate = s.DueDate,
            SubmissionDate = s.SubmissionDate, Status = s.Status, Marks = s.Marks, TotalMarks = s.TotalMarks,
            ReviewedBy = s.ReviewedBy, ReviewedAt = s.ReviewedAt, Feedback = s.Feedback, Remarks = s.Remarks
        });
        // Preserve encounter order for the legacy CLR stable sort, without holding the result set in memory.
        // Finish the database reader before lookup queries, since PostgreSQL permits one active command per connection.
        var batch = new List<LearningExportSubmission>(batchSize);
        long ordinal = 0;
        await foreach (var fact in AcademicReportExternalSort.SortAsync((IAsyncEnumerable<LearningExportSubmission>)query,
            (_, _) => 0, cancellationToken: cancellationToken))
        {
            fact.SourceOrdinal = ordinal++;
            batch.Add(fact);
            if (batch.Count < batchSize) continue;
            await HydrateLearningSubmissionBatchAsync(tasks, batch, cancellationToken);
            yield return batch;
            batch = new(batchSize);
        }
        if (batch.Count > 0)
        {
            await HydrateLearningSubmissionBatchAsync(tasks, batch, cancellationToken);
            yield return batch;
        }
    }

    private async Task HydrateLearningSubmissionBatchAsync(IQueryable<LearningExportTask> tasks,
        List<LearningExportSubmission> batch, CancellationToken cancellationToken)
    {
        var ids = batch.Select(s => s.Task.Id).Distinct().ToList();
        var taskMap = (await asyncExecuter.ToListAsync(tasks.Where(t => ids.Contains(t.Id)), cancellationToken)).ToDictionary(t => t.Id);
        batch.RemoveAll(s => !taskMap.ContainsKey(s.Task.Id));
        foreach (var fact in batch) fact.Task = taskMap[fact.Task.Id];
        await EnrichLearningExportAsync(batch, cancellationToken);
    }

    private async Task<IQueryable<LearningExportSubmission>> GetLearningExportSchedulesAsync(
        AcademicReportFilterDto filters, bool assignments, List<Guid>? taskIds = null)
    {
        var tasks = await GetLearningExportTasksAsync(filters, assignments);
        if (taskIds != null) tasks = tasks.Where(t => taskIds.Contains(t.Id));
        if (assignments)
        {
            var roots = await assignmentRepository.GetQueryableAsync();
            return from task in tasks
                   join root in roots on task.Id equals root.Id
                   from schedule in root.DivisionSchedules.DefaultIfEmpty()
                   select new LearningExportSubmission
                   {
                       Task = task, Id = schedule == null ? root.Id : schedule.Id,
                       DivisionId = schedule == null ? root.DivisionId : schedule.DivisionId,
                       DueDate = schedule == null ? DateTime.MaxValue : schedule.DueDate
                   };
        }
        else
        {
            var roots = await homeworkRepository.GetQueryableAsync();
            return from task in tasks
                   join root in roots on task.Id equals root.Id
                   from schedule in root.DivisionSchedules.DefaultIfEmpty()
                   where schedule != null || (root.DivisionId.HasValue && root.DueDate.HasValue)
                   select new LearningExportSubmission
                   {
                       Task = task, Id = schedule == null ? root.Id : schedule.Id,
                       DivisionId = schedule == null ? root.DivisionId!.Value : schedule.DivisionId,
                       DueDate = schedule == null ? root.DueDate!.Value : schedule.DueDate
                   };
        }
    }

    private async IAsyncEnumerable<LearningScheduleStat> ReadLearningScheduleStatsAsync(
        AcademicReportFilterDto filters, bool assignments, int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken, List<Guid>? taskIds = null)
    {
        var schedules = await GetLearningExportSchedulesAsync(filters, assignments, taskIds);
        var submissions = await GetLearningExportSubmissionsAsync(filters, assignments);
        var students = await studentRepository.GetQueryableAsync();
        for (var offset = 0; ; offset += batchSize)
        {
            var batch = await asyncExecuter.ToListAsync(schedules.OrderBy(s => s.Task.Id).ThenBy(s => s.Id)
                .Skip(offset).Take(batchSize), cancellationToken);
            if (batch.Count == 0) yield break;
            var rootIds = batch.Select(s => s.Task.Id).Distinct().ToList();
            var divisionIds = batch.Select(s => s.DivisionId).Distinct().ToList();
            var counts = (await asyncExecuter.ToListAsync(submissions.Where(s => rootIds.Contains(s.Task.Id) && divisionIds.Contains(s.DivisionId))
                .GroupBy(s => new { TaskId = s.Task.Id, s.DivisionId }).Select(g => new
                {
                    g.Key.TaskId, g.Key.DivisionId, Count = g.Count(), MarkedCount = g.Count(s => s.Marks.HasValue),
                    ScoreSum = g.Sum(s => s.Marks.HasValue && (s.TotalMarks ?? s.Task.TotalMarks) > 0
                        ? (double)s.Marks!.Value / (double)(s.TotalMarks ?? s.Task.TotalMarks) * 100 : 0),
                    Submitted = g.Count(s => s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late ||
                        s.Status == SubmissionStatus.Reviewed || s.Status == SubmissionStatus.Excused || s.Status == SubmissionStatus.NeedsReview)
                }), cancellationToken)).ToDictionary(s => (s.TaskId, s.DivisionId));
            var studentCounts = (await asyncExecuter.ToListAsync(students.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value))
                .GroupBy(s => s.DivisionId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }), cancellationToken)).ToDictionary(s => s.Id, s => s.Count);
            await EnrichLearningExportAsync(batch, cancellationToken);
            foreach (var source in batch)
            {
                counts.TryGetValue((source.Task.Id, source.DivisionId), out var count);
                yield return new(source, count?.Count ?? 0, count?.Submitted ?? 0, studentCounts.GetValueOrDefault(source.DivisionId),
                    count?.MarkedCount ?? 0, count?.ScoreSum ?? 0);
            }
            if (batch.Count < batchSize) yield break;
        }
    }

    private async IAsyncEnumerable<LearningRootStat> ReadLearningRootStatsAsync(AcademicReportFilterDto filters,
        bool assignments, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var roots = await GetLearningExportTasksAsync(filters, assignments);
        var submissions = await GetLearningExportSubmissionsAsync(filters, assignments);
        for (var offset = 0; ; offset += batchSize)
        {
            var tasks = await asyncExecuter.ToListAsync(roots.OrderBy(t => t.Id).Skip(offset).Take(batchSize), cancellationToken);
            if (tasks.Count == 0) yield break;
            var taskIds = tasks.Select(t => t.Id).ToList();
            var counts = (await asyncExecuter.ToListAsync(submissions.Where(s => taskIds.Contains(s.Task.Id)).GroupBy(s => s.Task.Id).Select(g => new
            {
                Id = g.Key, Count = g.Count(), Students = g.Select(s => s.StudentId).Distinct().Count(),
                Submitted = g.Count(s => s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late ||
                    s.Status == SubmissionStatus.Reviewed || s.Status == SubmissionStatus.Excused || s.Status == SubmissionStatus.NeedsReview)
            }), cancellationToken)).ToDictionary(s => s.Id);
            var totalStudents = new Dictionary<Guid, int>();
            await foreach (var schedule in ReadLearningScheduleStatsAsync(filters, assignments, batchSize, cancellationToken, taskIds))
                totalStudents[schedule.Source.Task.Id] = totalStudents.GetValueOrDefault(schedule.Source.Task.Id) + schedule.StudentCount;
            var batch = tasks.Select(t => new LearningExportSubmission { Task = t }).ToList();
            await EnrichLearningExportAsync(batch, cancellationToken);
            foreach (var source in batch)
            {
                counts.TryGetValue(source.Task.Id, out var count);
                yield return new(source, count?.Count ?? 0, count?.Submitted ?? 0, count?.Students ?? 0, totalStudents.GetValueOrDefault(source.Task.Id));
            }
            if (tasks.Count < batchSize) yield break;
        }
    }

    private async IAsyncEnumerable<LearningExportRow> BuildLearningScheduleRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var assignments = request.ReportType == AcademicReportTypes.AssignmentIssuanceReport;
        await foreach (var stat in ReadLearningScheduleStatsAsync(request.Filters, assignments, batchSize, cancellationToken))
        {
            var fact = stat.Source;
            var task = fact.Task;
            Dictionary<string, string?> row;
            if (request.ReportType == AcademicReportTypes.ClasswiseHomeworkReport)
                row = new() { ["Class"] = task.StandardName, ["Subject"] = task.SubjectName, ["HomeworkTitle"] = task.Title,
                    ["AssignedDate"] = task.CreationTime.ToString("yyyy-MM-dd"), ["DueDate"] = fact.DueDate.ToString("yyyy-MM-dd"),
                    ["Teacher"] = task.TeacherName, ["SubmissionRate"] = LearningPercent(stat.Submitted, stat.StudentCount) };
            else if (assignments)
                row = new() { ["AssignmentTitle"] = task.Title, ["Subject"] = task.SubjectName, ["Class"] = task.StandardName,
                    ["Division"] = fact.DivisionName, ["AssignedBy"] = task.TeacherName, ["AssignedDate"] = task.CreationTime.ToString("yyyy-MM-dd"),
                    ["DueDate"] = fact.DueDate == DateTime.MaxValue ? "—" : fact.DueDate.ToString("yyyy-MM-dd"),
                    ["TotalStudents"] = stat.StudentCount.ToString(), ["Status"] = ((AssignmentStatus)task.Status).ToString() };
            else
                row = new() { ["HomeworkDate"] = task.CreationTime.ToString("yyyy-MM-dd"), ["Class"] = task.StandardName,
                    ["Division"] = fact.DivisionName, ["Subject"] = task.SubjectName, ["HomeworkTitle"] = task.Title,
                    ["AssignedBy"] = task.TeacherName, ["DueDate"] = fact.DueDate.ToString("yyyy-MM-dd"),
                    ["TotalStudents"] = stat.StudentCount.ToString(), ["SubmissionCount"] = stat.Submitted.ToString(),
                    ["Status"] = ((HomeworkStatus)task.Status).ToString() };
            yield return new(fact, row);
        }
    }

    private async IAsyncEnumerable<LearningExportRow> BuildHomeworkCompletionRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var stat in ReadLearningRootStatsAsync(request.Filters, false, batchSize, cancellationToken))
            yield return new(stat.Source, new()
            {
                ["HomeworkTitle"] = stat.Source.Task.Title, ["Subject"] = stat.Source.Task.SubjectName,
                ["AssignedStudents"] = stat.DistinctStudents.ToString(), ["Submitted"] = stat.Submitted.ToString(),
                ["Pending"] = Math.Max(0, stat.DistinctStudents - stat.Submitted).ToString(),
                ["CompletionPercent"] = LearningPercent(stat.Submitted, stat.DistinctStudents)
            });
    }

    private static bool LearningSubmitted(SubmissionStatus status) => status is SubmissionStatus.Submitted or SubmissionStatus.Late
        or SubmissionStatus.Reviewed or SubmissionStatus.Excused or SubmissionStatus.NeedsReview;
    private static string LearningPercent(int numerator, int denominator) => $"{(denominator > 0 ? Math.Round((double)numerator / denominator * 100, 2) : 0)}%";
    private sealed record LearningScheduleStat(LearningExportSubmission Source, int Count, int Submitted, int StudentCount,
        int MarkedCount, double ScoreSum);
    private sealed record LearningRootStat(LearningExportSubmission Source, int Count, int Submitted, int DistinctStudents, int TotalStudentsAcrossSchedules);
}
