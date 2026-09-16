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
    private Task<AcademicReportExportRows?> TryCreateLearningAnalyticsExportAsync(AcademicReportRequestDto request,
        int batchSize, CancellationToken cancellationToken)
    {
        (string Id, string Label)[]? columns = request.ReportType switch
        {
            AcademicReportTypes.HomeworkLoadAnalysisReport => [("Class", "Class"), ("Division", "Division"), ("Subject", "Subject"),
                ("HomeworkCount", "Homework Count"), ("TotalQuestionsTasks", "Total Questions/Tasks"), ("AvgHomeworkPerWeek", "Average Homework Per Week"),
                ("AvgCompletionRate", "Average Completion Rate"), ("LoadStatus", "Load Status")],
            AcademicReportTypes.AssignmentLoadAnalysisReport => [("Class", "Class"), ("Division", "Division"), ("Subject", "Subject"),
                ("AssignmentCount", "Assignment Count"), ("TotalMarks", "Total Marks"), ("AvgAssignmentsPerMonth", "Average Assignments Per Month"),
                ("SubmissionRate", "Submission Rate"), ("LoadStatus", "Load Status")],
            AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport => [("Teacher", "Teacher"), ("HomeworkAssigned", "Homework Assigned"),
                ("HomeworkEvaluated", "Homework Evaluated"), ("PendingEvaluation", "Pending Evaluation"), ("AvgEvaluationTime", "Average Evaluation Time"),
                ("EvaluationCompletionPercent", "Evaluation Completion %")],
            AcademicReportTypes.StudentPerformanceTrendReport => [("Student", "Student"), ("Subject", "Subject"), ("HomeworkAverage", "Homework Average"),
                ("AssignmentAverage", "Assignment Average"), ("PreviousPeriodScore", "Previous Period Score"), ("CurrentPeriodScore", "Current Period Score"), ("GrowthPercent", "Growth %")],
            AcademicReportTypes.ClasswiseOverallHomeworkAssignmentReport => [("Class", "Class"), ("Division", "Division"), ("TotalHomework", "Total Homework"),
                ("TotalAssignments", "Total Assignments"), ("HomeworkCompletionPercent", "Homework Completion %"), ("AssignmentSubmissionPercent", "Assignment Submission %"),
                ("AverageMarks", "Average Marks"), ("OverallAcademicScore", "Overall Academic Score")],
            _ => null
        };
        if (columns == null) return Task.FromResult<AcademicReportExportRows?>(null);
        var metadata = NewExamExportMetadata(request);
        metadata.Columns = columns.Select(column => new AcademicReportColumnDto { Id = column.Id, Label = column.Label }).ToList();
        var rows = request.ReportType switch
        {
            AcademicReportTypes.HomeworkLoadAnalysisReport or AcademicReportTypes.AssignmentLoadAnalysisReport => ReadLearningLoadRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport => ReadLearningTeacherRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.StudentPerformanceTrendReport => ReadLearningTrendRowsAsync(request, batchSize, cancellationToken),
            _ => ReadLearningOverallRowsAsync(request, batchSize, cancellationToken)
        };
        return Task.FromResult<AcademicReportExportRows?>(new(metadata, rows));
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadLearningLoadRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = BuildLearningLoadRowsAsync(request, batchSize, cancellationToken);
        await foreach (var row in AcademicReportExternalSort.SortAsync(rows, CompareLearningAnalyticsNames, cancellationToken: cancellationToken)) yield return row;
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> BuildLearningLoadRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var assignments = request.ReportType == AcademicReportTypes.AssignmentLoadAnalysisReport;
        var tasks = await GetLearningExportTasksAsync(request.Filters, assignments);
        var dates = await asyncExecuter.FirstOrDefaultAsync(tasks.GroupBy(_ => 1)
            .Select(group => new { First = group.Min(task => task.CreationTime), Last = group.Max(task => task.CreationTime) }), cancellationToken);
        if (dates == null) yield break;
        var span = request.Filters.FromDate.HasValue && request.Filters.ToDate.HasValue
            ? request.Filters.ToDate.Value - request.Filters.FromDate.Value : dates.Last - dates.First;
        var periods = Math.Max(1, span.TotalDays / (assignments ? 30.44 : 7));
        var source = ReadLearningScheduleStatsAsync(request.Filters, assignments, batchSize, cancellationToken);
        LearningLoadAccumulator? group = null;
        await foreach (var stat in AcademicReportExternalSort.SortAsync(source, (a, b) =>
        {
            var order = CompareLearningScheduleKeys(a.Source, b.Source, includeSubject: true);
            return order != 0 ? order : a.Source.Task.Id.CompareTo(b.Source.Task.Id);
        }, cancellationToken: cancellationToken))
        {
            if (group != null && CompareLearningScheduleKeys(group.First.Source, stat.Source, includeSubject: true) != 0)
            {
                yield return BuildLearningLoadRow(group, periods, assignments);
                group = null;
            }
            group ??= new LearningLoadAccumulator(stat);
            group.Add(stat);
        }
        if (group != null) yield return BuildLearningLoadRow(group, periods, assignments);
    }

    private static Dictionary<string, string?> BuildLearningLoadRow(LearningLoadAccumulator group, double periods, bool assignments)
    {
        var source = group.First.Source;
        var average = Math.Round(group.Tasks / periods, 2);
        var row = new Dictionary<string, string?> { ["Class"] = source.Task.StandardName, ["Division"] = source.DivisionName, ["Subject"] = source.Task.SubjectName };
        if (assignments)
        {
            row["AssignmentCount"] = group.Tasks.ToString(); row["TotalMarks"] = group.TotalMarks.ToString("F0");
            row["AvgAssignmentsPerMonth"] = average.ToString("F2"); row["SubmissionRate"] = LearningPercent(group.Submitted, group.Submissions);
            row["LoadStatus"] = ComputeLoadStatus(average, 2, 4);
        }
        else
        {
            row["HomeworkCount"] = group.Tasks.ToString(); row["TotalQuestionsTasks"] = "—";
            row["AvgHomeworkPerWeek"] = average.ToString("F2"); row["AvgCompletionRate"] = $"{Math.Round(group.CompletionRateSum / group.Tasks, 2)}%";
            row["LoadStatus"] = ComputeLoadStatus(average, 3, 7);
        }
        return row;
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadLearningOverallRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = BuildLearningOverallRowsAsync(request, batchSize, cancellationToken);
        await foreach (var row in AcademicReportExternalSort.SortAsync(rows, CompareLearningAnalyticsNames, cancellationToken: cancellationToken)) yield return row;
    }

    private async IAsyncEnumerable<LearningOverallFact> ReadLearningOverallFactsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var assignments in new[] { false, true })
            await foreach (var stat in ReadLearningScheduleStatsAsync(request.Filters, assignments, batchSize, cancellationToken))
                yield return new LearningOverallFact(assignments, stat);
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> BuildLearningOverallRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LearningLoadAccumulator? homework = null;
        LearningLoadAccumulator? assignment = null;
        LearningExportSubmission? first = null;
        await foreach (var fact in AcademicReportExternalSort.SortAsync(ReadLearningOverallFactsAsync(request, batchSize, cancellationToken), (a, b) =>
        {
            var order = CompareLearningScheduleKeys(a.Stat.Source, b.Stat.Source, includeSubject: false);
            if (order == 0) order = a.Assignment.CompareTo(b.Assignment);
            return order != 0 ? order : a.Stat.Source.Task.Id.CompareTo(b.Stat.Source.Task.Id);
        }, cancellationToken: cancellationToken))
        {
            if (first != null && CompareLearningScheduleKeys(first, fact.Stat.Source, includeSubject: false) != 0)
            {
                yield return BuildLearningOverallRow(first, homework, assignment);
                first = null; homework = null; assignment = null;
            }
            first ??= fact.Stat.Source;
            if (fact.Assignment) { assignment ??= new(fact.Stat); assignment.Add(fact.Stat); }
            else { homework ??= new(fact.Stat); homework.Add(fact.Stat); }
        }
        if (first != null) yield return BuildLearningOverallRow(first, homework, assignment);
    }

    private static Dictionary<string, string?> BuildLearningOverallRow(LearningExportSubmission first, LearningLoadAccumulator? homework, LearningLoadAccumulator? assignment)
    {
        var homeworkPercent = LearningRate(homework?.Submitted ?? 0, homework?.Submissions ?? 0);
        var assignmentPercent = LearningRate(assignment?.Submitted ?? 0, assignment?.Submissions ?? 0);
        var marks = assignment is { MarkedCount: > 0 } ? Math.Round(assignment.ScoreSum / assignment.MarkedCount, 2) : 0;
        return new() { ["Class"] = first.Task.StandardName, ["Division"] = first.DivisionName,
            ["TotalHomework"] = (homework?.Tasks ?? 0).ToString(), ["TotalAssignments"] = (assignment?.Tasks ?? 0).ToString(),
            ["HomeworkCompletionPercent"] = $"{homeworkPercent}%", ["AssignmentSubmissionPercent"] = $"{assignmentPercent}%",
            ["AverageMarks"] = assignment is { MarkedCount: > 0 } ? $"{marks}%" : "—",
            ["OverallAcademicScore"] = $"{Math.Round((homeworkPercent + assignmentPercent + marks) / 3, 2)}%" };
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadLearningTeacherRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in AcademicReportExternalSort.SortAsync(BuildLearningTeacherRowsAsync(request, batchSize, cancellationToken),
            (a, b) => Comparer<string?>.Default.Compare(a["Teacher"], b["Teacher"]), cancellationToken: cancellationToken)) yield return row;
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> BuildLearningTeacherRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = await GetLearningExportTasksAsync(request.Filters, false);
        var creators = tasks.Where(task => task.CreatorId.HasValue).GroupBy(task => task.CreatorId!.Value)
            .Select(group => new { Id = group.Key, Tasks = group.Count() }).OrderBy(group => group.Id);
        var submissions = await GetLearningExportSubmissionsAsync(request.Filters, false);
        var employees = await employeeRepository.GetQueryableAsync();
        for (var offset = 0; ; offset += batchSize)
        {
            var batch = await asyncExecuter.ToListAsync(creators.Skip(offset).Take(batchSize), cancellationToken);
            if (batch.Count == 0) yield break;
            var ids = batch.Select(creator => creator.Id).ToList();
            var names = (await asyncExecuter.ToListAsync(employees.Where(employee => employee.UserId.HasValue && ids.Contains(employee.UserId.Value))
                .Select(employee => new { employee.UserId, employee.FirstName, employee.LastName }), cancellationToken))
                .ToDictionary(employee => employee.UserId!.Value, employee => string.Join(" ", new[] { employee.FirstName, employee.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))));
            var stats = ids.ToDictionary(id => id, _ => new LearningTeacherAccumulator());
            var selected = submissions.Where(submission => submission.Task.CreatorId.HasValue && ids.Contains(submission.Task.CreatorId.Value));
            for (var submissionOffset = 0; ; submissionOffset += batchSize)
            {
                var values = await asyncExecuter.ToListAsync(selected.OrderBy(submission => submission.Id).Skip(submissionOffset).Take(batchSize), cancellationToken);
                if (values.Count == 0) break;
                foreach (var value in values)
                {
                    var stat = stats[value.Task.CreatorId!.Value];
                    if (value.Status is SubmissionStatus.Submitted or SubmissionStatus.NeedsReview or SubmissionStatus.Late) stat.Pending++;
                    if (value.Status != SubmissionStatus.Reviewed || !value.ReviewedBy.HasValue) continue;
                    stat.Evaluated++;
                    if (value.ReviewedAt.HasValue && value.SubmissionDate.HasValue)
                    { stat.Hours += (value.ReviewedAt.Value - value.SubmissionDate.Value).TotalHours; stat.Timed++; }
                }
                if (values.Count < batchSize) break;
            }
            foreach (var creator in batch)
            {
                var stat = stats[creator.Id];
                var hours = stat.Timed > 0 ? Math.Round(stat.Hours / stat.Timed, 1) : 0;
                yield return new() { ["Teacher"] = names.GetValueOrDefault(creator.Id, "—"), ["HomeworkAssigned"] = creator.Tasks.ToString(),
                    ["HomeworkEvaluated"] = stat.Evaluated.ToString(), ["PendingEvaluation"] = stat.Pending.ToString(),
                    ["AvgEvaluationTime"] = stat.Timed == 0 ? "—" : hours < 24 ? $"{hours} hrs" : $"{Math.Round(hours / 24, 1)} days",
                    ["EvaluationCompletionPercent"] = LearningPercent(stat.Evaluated, stat.Evaluated + stat.Pending) };
            }
            if (batch.Count < batchSize) yield break;
        }
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadLearningTrendRowsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Filters.StudentIds is not { Count: > 0 }) yield break;
        LearningTrendAccumulator? group = null;
        await foreach (var fact in AcademicReportExternalSort.SortAsync(ReadLearningTrendFactsAsync(request, batchSize, cancellationToken), (a, b) =>
        {
            var order = a.StudentOrder.CompareTo(b.StudentOrder);
            return order != 0 ? order : a.SubjectOrder.CompareTo(b.SubjectOrder);
        }, cancellationToken: cancellationToken))
        {
            if (group != null && (group.First.StudentOrder != fact.StudentOrder || group.First.SubjectOrder != fact.SubjectOrder))
            {
                if (group.Homework > 0 || group.Marked > 0) yield return BuildLearningTrendRow(group);
                group = null;
            }
            group ??= new LearningTrendAccumulator(fact);
            if (!fact.Assignment)
            { group.Homework++; if (LearningSubmitted(fact.Source.Status)) group.Submitted++; continue; }
            if (!fact.Source.Marks.HasValue) continue;
            var total = (double)(fact.Source.TotalMarks ?? fact.Source.Task.TotalMarks);
            var score = total > 0 ? (double)fact.Source.Marks.Value / total * 100 : 0;
            group.Marked++; group.Score += score;
            if (fact.Period < 0) { group.Previous++; group.PreviousScore += score; }
            if (fact.Period > 0) { group.Current++; group.CurrentScore += score; }
        }
        if (group != null && (group.Homework > 0 || group.Marked > 0)) yield return BuildLearningTrendRow(group);
    }

    private async IAsyncEnumerable<LearningTrendFact> ReadLearningTrendFactsAsync(AcademicReportRequestDto request,
        int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var studentIds = request.Filters.StudentIds;
        if (studentIds is not { Count: > 0 }) yield break;
        var students = studentIds.Select((id, index) => (id, index)).GroupBy(value => value.id)
            .ToDictionary(group => group.Key, group => group.Select(value => value.index).ToList());
        var subjects = new Dictionary<Guid, int>();
        var midpoint = request.Filters.FromDate.HasValue && request.Filters.ToDate.HasValue
            ? request.Filters.FromDate.Value + (request.Filters.ToDate.Value - request.Filters.FromDate.Value) / 2 : (DateTime?)null;
        foreach (var assignments in new[] { false, true })
        {
            await foreach (var batch in ReadLearningSubmissionBatchesAsync(request.Filters, assignments, batchSize, cancellationToken))
            {
                foreach (var source in batch)
                {
                    if (!subjects.TryGetValue(source.Task.SubjectId, out var subjectOrder)) subjects[source.Task.SubjectId] = subjectOrder = subjects.Count;
                    if (source.StudentName == null) continue;
                    var period = midpoint.HasValue && source.SubmissionDate.HasValue ? source.SubmissionDate.Value.UtcDateTime < midpoint.Value ? -1 : 1 : 0;
                    foreach (var studentOrder in students[source.StudentId])
                        yield return new(source, assignments, studentOrder, subjectOrder, period);
                }
            }
        }
    }

    private static Dictionary<string, string?> BuildLearningTrendRow(LearningTrendAccumulator group)
    {
        var previous = group.Previous > 0 ? Math.Round(group.PreviousScore / group.Previous, 2) : 0;
        var current = group.Current > 0 ? Math.Round(group.CurrentScore / group.Current, 2) : 0;
        return new() { ["Student"] = group.First.Source.StudentName, ["Subject"] = group.First.Source.Task.SubjectName,
            ["HomeworkAverage"] = group.Homework > 0 ? LearningPercent(group.Submitted, group.Homework) : "—",
            ["AssignmentAverage"] = group.Marked > 0 ? $"{Math.Round(group.Score / group.Marked, 2)}%" : "—",
            ["PreviousPeriodScore"] = group.Previous > 0 ? $"{previous}%" : "—", ["CurrentPeriodScore"] = group.Current > 0 ? $"{current}%" : "—",
            ["GrowthPercent"] = group.Previous > 0 && group.Current > 0 && previous > 0 ? $"{Math.Round((current - previous) / previous * 100, 2)}%" : "—" };
    }

    private static int CompareLearningScheduleKeys(LearningExportSubmission a, LearningExportSubmission b, bool includeSubject)
    {
        var order = a.Task.StandardId.CompareTo(b.Task.StandardId);
        if (order == 0) order = a.DivisionId.CompareTo(b.DivisionId);
        if (order == 0 && includeSubject) order = a.Task.SubjectId.CompareTo(b.Task.SubjectId);
        return order;
    }

    private static int CompareLearningAnalyticsNames(Dictionary<string, string?> a, Dictionary<string, string?> b)
    {
        var strings = Comparer<string?>.Default;
        foreach (var key in new[] { "Class", "Division", "Subject" })
        {
            var order = strings.Compare(a.GetValueOrDefault(key), b.GetValueOrDefault(key));
            if (order != 0) return order;
        }
        return 0;
    }

    private static double LearningRate(int numerator, int denominator) => denominator > 0 ? Math.Round((double)numerator / denominator * 100, 2) : 0;
    private sealed record LearningOverallFact(bool Assignment, LearningScheduleStat Stat);
    private sealed record LearningTrendFact(LearningExportSubmission Source, bool Assignment, int StudentOrder, int SubjectOrder, int Period);
    private sealed class LearningLoadAccumulator(LearningScheduleStat first)
    {
        public LearningScheduleStat First { get; } = first;
        private Guid? _lastTask;
        public int Tasks, Submissions, Submitted, MarkedCount;
        public double TotalMarks, CompletionRateSum, ScoreSum;
        public void Add(LearningScheduleStat stat)
        {
            if (_lastTask == stat.Source.Task.Id) return;
            _lastTask = stat.Source.Task.Id; Tasks++;
            Submissions += stat.Count; Submitted += stat.Submitted; TotalMarks += (double)stat.Source.Task.TotalMarks;
            CompletionRateSum += stat.Count > 0 ? (double)stat.Submitted / stat.Count * 100 : 0;
            MarkedCount += stat.MarkedCount; ScoreSum += stat.ScoreSum;
        }
    }
    private sealed class LearningTeacherAccumulator { public int Evaluated, Pending, Timed; public double Hours; }
    private sealed class LearningTrendAccumulator(LearningTrendFact first)
    {
        public LearningTrendFact First { get; } = first;
        public int Homework, Submitted, Marked, Previous, Current;
        public double Score, PreviousScore, CurrentScore;
    }
}
