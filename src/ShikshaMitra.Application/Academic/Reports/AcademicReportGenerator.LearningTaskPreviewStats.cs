using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<List<LearningExportTask>> LoadLearningPreviewTaskKeysAsync(IQueryable<LearningExportTask> tasks) =>
        await asyncExecuter.ToListAsync(ProjectLearningPreviewTasks(tasks).OrderBy(task => task.Id));

    private async Task<List<LearningExportSubmission>> LoadLearningPreviewScheduleKeysAsync(AcademicReportFilterDto filters, bool assignments,
        Guid[]? taskIds = null)
    {
        var schedules = await GetLearningExportSchedulesAsync(filters, assignments);
        if (taskIds != null) schedules = schedules.Where(source => Enumerable.Contains(taskIds, source.Task.Id));
        return await asyncExecuter.ToListAsync(schedules.OrderBy(source => source.Task.Id).ThenBy(source => source.Id)
            .Select(source => new LearningExportSubmission
            {
                Id = source.Id, DivisionId = source.DivisionId, DueDate = source.DueDate,
                Task = new LearningExportTask
                {
                    Id = source.Task.Id, StandardId = source.Task.StandardId, SubjectId = source.Task.SubjectId,
                    CreatorId = source.Task.CreatorId, CreationTime = source.Task.CreationTime,
                    Title = source.Task.Title, Status = source.Task.Status, TotalMarks = source.Task.TotalMarks
                }
            }));
    }

    private async Task<List<LearningPreviewStats>> ReadLearningPreviewStatsAsync(AcademicReportFilterDto filters, bool assignments,
        Guid[] taskIds, Guid[]? divisionIds = null, bool byDivision = false)
    {
        var source = await GetLearningExportSubmissionsAsync(filters, assignments);
        source = source.Where(submission => Enumerable.Contains(taskIds, submission.Task.Id));
        if (divisionIds != null) source = source.Where(submission => Enumerable.Contains(divisionIds, submission.DivisionId));
        var today = DateTime.UtcNow.Date;
        return await asyncExecuter.ToListAsync(source.GroupBy(submission => new
        {
            TaskId = submission.Task.Id, DivisionId = byDivision ? submission.DivisionId : Guid.Empty
        }).Select(group => new LearningPreviewStats
        {
            TaskId = group.Key.TaskId, DivisionId = group.Key.DivisionId,
            Count = group.Count(), DistinctStudents = group.Select(submission => submission.StudentId).Distinct().Count(),
            Submitted = group.Count(submission => submission.Status == SubmissionStatus.Submitted || submission.Status == SubmissionStatus.Late ||
                submission.Status == SubmissionStatus.Reviewed || submission.Status == SubmissionStatus.Excused || submission.Status == SubmissionStatus.NeedsReview),
            Pending = group.Count(submission => (submission.Status == SubmissionStatus.NotSubmitted || submission.Status == SubmissionStatus.Draft) && submission.DueDate.Date >= today),
            Overdue = group.Count(submission => (submission.Status == SubmissionStatus.NotSubmitted || submission.Status == SubmissionStatus.Draft) && submission.DueDate.Date < today),
            Evaluated = group.Count(submission => submission.Status == SubmissionStatus.Reviewed),
            EvaluatedWithReviewer = group.Count(submission => submission.Status == SubmissionStatus.Reviewed && submission.ReviewedBy.HasValue),
            PendingEvaluation = group.Count(submission => submission.Status == SubmissionStatus.Submitted ||
                submission.Status == SubmissionStatus.NeedsReview || submission.Status == SubmissionStatus.Late),
            Marked = group.Count(submission => submission.Marks.HasValue),
            ScoreSum = group.Sum(submission => submission.Marks.HasValue && (submission.TotalMarks ?? submission.Task.TotalMarks) > 0
                ? (double)submission.Marks!.Value / (double)(submission.TotalMarks ?? submission.Task.TotalMarks) * 100 : 0),
            Highest = group.Max(submission => (double?)submission.Marks) ?? 0,
            Lowest = group.Min(submission => (double?)submission.Marks) ?? 0
        }));
    }

    private async Task<Dictionary<Guid, int>> ReadLearningPreviewStudentCountsAsync(IEnumerable<Guid> divisions)
    {
        var ids = divisions.Distinct().ToArray();
        var students = await studentRepository.GetQueryableAsync();
        var counts = await asyncExecuter.ToListAsync(students.Where(student => student.DivisionId.HasValue &&
                Enumerable.Contains(ids, student.DivisionId.Value))
            .GroupBy(student => student.DivisionId!.Value).Select(group => new { Id = group.Key, Count = group.Count() }));
        return counts.ToDictionary(row => row.Id, row => row.Count);
    }

    private static List<LearningPreviewGroup> SelectLearningPreviewGroups(List<LearningPreviewGroup> groups,
        AcademicReportRequestDto request)
    {
        var bySource = groups.ToDictionary(group => group.Source);
        return SelectLearningPreviewPage(groups.Select(group => group.Source), request).Select(source => bySource[source]).ToList();
    }

    private sealed record LearningPreviewGroup(LearningExportSubmission Source, List<LearningExportTask> Tasks,
        List<LearningExportSubmission>? Schedules = null, string? Grade = null);

    private sealed class LearningPreviewStats
    {
        public Guid TaskId { get; init; }
        public Guid DivisionId { get; init; }
        public int Count { get; init; }
        public int DistinctStudents { get; init; }
        public int Submitted { get; init; }
        public int Pending { get; init; }
        public int Overdue { get; init; }
        public int Evaluated { get; init; }
        public int EvaluatedWithReviewer { get; init; }
        public int PendingEvaluation { get; init; }
        public int Marked { get; init; }
        public double ScoreSum { get; init; }
        public double Highest { get; init; }
        public double Lowest { get; init; }
    }
}
