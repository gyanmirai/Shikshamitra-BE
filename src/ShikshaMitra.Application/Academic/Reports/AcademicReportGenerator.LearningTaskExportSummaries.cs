using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async IAsyncEnumerable<LearningSummaryFact> ReadLearningSummaryFactsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kind = request.ReportType;
        var assignments = kind is AcademicReportTypes.AssignmentEvaluationReport or AcademicReportTypes.AssignmentGradeDistributionReport
            or AcademicReportTypes.AssignmentPerformanceReport;
        if (kind == AcademicReportTypes.SubjectwiseHomeworkReport)
        {
            await foreach (var root in ReadLearningRootStatsAsync(request.Filters, false, batchSize, cancellationToken))
                yield return new(root.Source, true, root.Count, root.Submitted, root.TotalStudentsAcrossSchedules);
        }
        else if (kind is AcademicReportTypes.AssignmentEvaluationReport or AcademicReportTypes.AssignmentPerformanceReport)
        {
            var roots = await GetLearningExportTasksAsync(request.Filters, true);
            for (var offset = 0; ; offset += batchSize)
            {
                var batch = (await asyncExecuter.ToListAsync(roots.OrderBy(t => t.Id).Skip(offset).Take(batchSize), cancellationToken))
                    .Select(t => new LearningExportSubmission { Task = t }).ToList();
                if (batch.Count == 0) break;
                await EnrichLearningExportAsync(batch, cancellationToken);
                foreach (var source in batch) yield return new(source, true);
                if (batch.Count < batchSize) break;
            }
        }
        await foreach (var batch in ReadLearningSubmissionBatchesAsync(request.Filters, assignments, batchSize, cancellationToken))
        {
            foreach (var source in batch)
            {
                if (kind == AcademicReportTypes.AssignmentGradeDistributionReport && !source.Marks.HasValue) continue;
                yield return new(source, false);
            }
        }
    }

    private async IAsyncEnumerable<LearningExportRow> BuildLearningSummaryRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kind = request.ReportType;
        var source = ReadLearningSummaryFactsAsync(request, batchSize, cancellationToken);
        LearningSummaryAccumulator? accumulator = null;
        var today = DateTime.UtcNow.Date;
        await foreach (var fact in AcademicReportExternalSort.SortAsync(source,
            (a, b) => CompareLearningSummaryFacts(a, b, kind), cancellationToken: cancellationToken))
        {
            if (accumulator != null && LearningSummaryKey(accumulator.First, kind) != LearningSummaryKey(fact.Source, kind))
            {
                foreach (var row in FinishLearningSummary(accumulator, kind)) yield return row;
                accumulator = null;
            }
            accumulator ??= new(fact.Source);
            accumulator.Add(fact, kind, today);
        }
        if (accumulator != null)
            foreach (var row in FinishLearningSummary(accumulator, kind)) yield return row;
    }

    private static (Guid First, Guid Second) LearningSummaryKey(LearningExportSubmission source, string kind) => kind switch
    {
        AcademicReportTypes.SubjectwiseHomeworkReport => (source.Task.SubjectId, Guid.Empty),
        AcademicReportTypes.StudentwiseHomeworkSummaryReport => (source.StudentId, source.Task.Id == Guid.Empty ? Guid.Empty : source.Task.SubjectId),
        AcademicReportTypes.AssignmentPerformanceReport => (source.Task.StandardId, source.Task.SubjectId),
        _ => (source.Task.Id, Guid.Empty)
    };

    private static int CompareLearningSummaryFacts(LearningSummaryFact a, LearningSummaryFact b, string kind)
    {
        var key = LearningSummaryKey(a.Source, kind).CompareTo(LearningSummaryKey(b.Source, kind));
        if (key != 0) return key;
        if (kind == AcademicReportTypes.SubjectwiseHomeworkReport)
        {
            key = b.IsTask.CompareTo(a.IsTask);
            if (key == 0 && !a.IsTask) key = a.Source.StudentId.CompareTo(b.Source.StudentId);
        }
        if (kind == AcademicReportTypes.AssignmentPerformanceReport || (a.IsTask && b.IsTask))
            if (key == 0) key = a.Source.Task.Id.CompareTo(b.Source.Task.Id);
        if (key == 0) key = b.IsTask.CompareTo(a.IsTask);
        if (key == 0) key = a.Source.SourceOrdinal.CompareTo(b.Source.SourceOrdinal);
        return key;
    }

    private static IEnumerable<LearningExportRow> FinishLearningSummary(LearningSummaryAccumulator state, string kind)
    {
        var source = state.First;
        var task = source.Task;
        Dictionary<string, string?> row;
        switch (kind)
        {
            case AcademicReportTypes.SubjectwiseHomeworkReport:
                row = new() { ["Subject"] = task.SubjectName, ["StudentCount"] = state.DistinctStudents.ToString(),
                    ["HomeworkCount"] = state.Tasks.ToString(), ["AssignedTeacher"] = state.Names.Count > 0 ? string.Join(", ", state.Names) : "—",
                    ["AverageSubmissionRate"] = $"{(state.Tasks > 0 ? Math.Round(state.RateSum / state.Tasks, 2) : 0)}%",
                    ["CompletionRate"] = LearningPercent(state.Submitted, state.Count) };
                break;
            case AcademicReportTypes.StudentwiseHomeworkSummaryReport:
                if (source.StudentName == null) yield break;
                row = new() { ["StudentName"] = source.StudentName, ["Class"] = task.StandardName, ["Division"] = source.DivisionName,
                    ["Subject"] = task.SubjectName, ["AssignedHomework"] = state.Count.ToString(), ["Submitted"] = state.Submitted.ToString(),
                    ["Pending"] = state.Pending.ToString(), ["Overdue"] = state.Overdue.ToString(), ["CompletionPercent"] = LearningPercent(state.Submitted, state.Count) };
                break;
            case AcademicReportTypes.AssignmentEvaluationReport:
                row = new() { ["Assignment"] = task.Title, ["EvaluatedBy"] = state.Names.Count > 0 ? string.Join(", ", state.Names) : "—",
                    ["Subject"] = task.SubjectName, ["EvaluationDate"] = state.LatestReview == default ? "—" : state.LatestReview.ToString("yyyy-MM-dd"),
                    ["StudentsEvaluated"] = state.Evaluated.ToString(), ["PendingEvaluations"] = state.Pending.ToString(), ["Status"] = ((AssignmentStatus)task.Status).ToString() };
                break;
            case AcademicReportTypes.AssignmentPerformanceReport:
                row = new() { ["Class"] = task.StandardName, ["Subject"] = task.SubjectName, ["TotalAssignments"] = state.Tasks.ToString(),
                    ["AverageScore"] = $"{(state.Marked > 0 ? Math.Round(state.RateSum / state.Marked, 2) : 0)}%",
                    ["HighestScore"] = (state.Marked > 0 ? state.Highest : 0).ToString("F2"),
                    ["LowestScore"] = (state.Marked > 0 ? state.Lowest : 0).ToString("F2"), ["CompletionRate"] = LearningPercent(state.Submitted, state.Count) };
                break;
            case AcademicReportTypes.AssignmentGradeDistributionReport:
                foreach (var grade in new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F" })
                {
                    if (!state.Grades.TryGetValue(grade, out var marks)) continue;
                    yield return new(source, new()
                    {
                        ["Grade"] = grade, ["Assignment"] = task.Title, ["StudentCount"] = marks.Count.ToString(),
                        ["Percentage"] = LearningPercent(marks.Count, state.Count), ["HighestMarks"] = marks.Highest.ToString("F2"),
                        ["LowestMarks"] = marks.Lowest.ToString("F2")
                    });
                }
                yield break;
            default: throw new InvalidOperationException($"Unsupported learning task summary: {kind}");
        }
        yield return new(source, row);
    }

    private async IAsyncEnumerable<LearningExportRow> BuildTeacherwiseHomeworkRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        static (Guid? Creator, Guid Subject, Guid Standard, Guid Division) Key(LearningScheduleStat s) =>
            (s.Source.Task.CreatorId, s.Source.Task.SubjectId, s.Source.Task.StandardId, s.Source.DivisionId);
        static LearningExportRow Finish(LearningScheduleStat first, int tasks, int submitted) => new(first.Source, new()
        {
            ["Teacher"] = first.Source.Task.TeacherName, ["Subject"] = first.Source.Task.SubjectName,
            ["Class"] = first.Source.Task.StandardName, ["Division"] = first.Source.DivisionName,
            ["HomeworkAssigned"] = tasks.ToString(), ["TotalStudents"] = first.StudentCount.ToString(),
            ["SubmissionRate"] = LearningPercent(submitted, first.StudentCount * tasks)
        });
        LearningScheduleStat? first = null;
        var tasks = 0;
        var submitted = 0;
        Guid? previousTask = null;
        await foreach (var stat in AcademicReportExternalSort.SortAsync(
            ReadLearningScheduleStatsAsync(request.Filters, false, batchSize, cancellationToken),
            (a, b) => Key(a).CompareTo(Key(b)) is var compare && compare != 0 ? compare : a.Source.Task.Id.CompareTo(b.Source.Task.Id),
            cancellationToken: cancellationToken))
        {
            if (first != null && Key(first) != Key(stat))
            {
                yield return Finish(first, tasks, submitted);
                first = null;
                tasks = 0;
                submitted = 0;
                previousTask = null;
            }
            first ??= stat;
            if (previousTask == stat.Source.Task.Id) continue;
            previousTask = stat.Source.Task.Id;
            tasks++;
            submitted += stat.Submitted;
        }
        if (first != null) yield return Finish(first, tasks, submitted);
    }

    private sealed record LearningSummaryFact(LearningExportSubmission Source, bool IsTask,
        int Count = 0, int Submitted = 0, int Students = 0);

    private sealed class LearningSummaryAccumulator(LearningExportSubmission first)
    {
        public LearningExportSubmission First { get; } = first;
        public int Tasks { get; private set; }
        public int Count { get; private set; }
        public int Submitted { get; private set; }
        public int Pending { get; private set; }
        public int Overdue { get; private set; }
        public int Evaluated { get; private set; }
        public int Marked { get; private set; }
        public int DistinctStudents { get; private set; }
        public double RateSum { get; private set; }
        public double Highest { get; private set; } = double.MinValue;
        public double Lowest { get; private set; } = double.MaxValue;
        public DateTimeOffset LatestReview { get; private set; }
        public List<string> Names { get; } = [];
        public Dictionary<string, (int Count, double Highest, double Lowest)> Grades { get; } = [];
        private readonly HashSet<string> _names = [];
        private Guid? _previousStudent;

        public void Add(LearningSummaryFact fact, string kind, DateTime today)
        {
            var source = fact.Source;
            if (fact.IsTask)
            {
                Tasks++;
                if (kind == AcademicReportTypes.SubjectwiseHomeworkReport)
                {
                    Count += fact.Count;
                    Submitted += fact.Submitted;
                    RateSum += fact.Students > 0 ? (double)fact.Submitted / fact.Students * 100 : 0;
                    AddName(source.Task.TeacherSortName);
                }
                return;
            }
            if (kind == AcademicReportTypes.SubjectwiseHomeworkReport)
            {
                if (_previousStudent != source.StudentId) DistinctStudents++;
                _previousStudent = source.StudentId;
                return;
            }
            Count++;
            if (LearningSubmitted(source.Status)) Submitted++;
            if (kind == AcademicReportTypes.StudentwiseHomeworkSummaryReport && source.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft)
            {
                if (source.DueDate.Date < today) Overdue++;
                else Pending++;
            }
            if (kind == AcademicReportTypes.AssignmentEvaluationReport)
            {
                if (source.Status == SubmissionStatus.Reviewed)
                {
                    Evaluated++;
                    if (source.ReviewedAt.HasValue && source.ReviewedAt.Value > LatestReview) LatestReview = source.ReviewedAt.Value;
                    AddName(source.ReviewerSortName);
                }
                if (source.Status is SubmissionStatus.Submitted or SubmissionStatus.NeedsReview or SubmissionStatus.Late) Pending++;
            }
            if (source.Marks.HasValue)
            {
                Marked++;
                var obtained = (double)source.Marks.Value;
                var total = (double)(source.TotalMarks ?? source.Task.TotalMarks);
                RateSum += total > 0 ? obtained / total * 100 : 0;
                Highest = Math.Max(Highest, obtained);
                Lowest = Math.Min(Lowest, obtained);
                if (kind == AcademicReportTypes.AssignmentGradeDistributionReport)
                {
                    var grade = ComputeGrade(obtained, total);
                    var previous = Grades.GetValueOrDefault(grade, (0, double.MinValue, double.MaxValue));
                    Grades[grade] = (previous.Item1 + 1, Math.Max(previous.Item2, obtained), Math.Min(previous.Item3, obtained));
                }
            }
        }

        private void AddName(string name)
        {
            if (!string.IsNullOrEmpty(name) && _names.Add(name)) Names.Add(name);
        }
    }
}
