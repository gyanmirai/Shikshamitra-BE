using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto> BuildLearningSummaryPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload)
    {
        var kind = request.ReportType;
        var assignments = kind is AcademicReportTypes.AssignmentEvaluationReport or AcademicReportTypes.AssignmentGradeDistributionReport
            or AcademicReportTypes.AssignmentPerformanceReport;
        var tasks = await GetLearningExportTasksAsync(request.Filters, assignments);
        var roots = await LoadLearningPreviewTaskKeysAsync(tasks);
        if (roots.Count == 0) return payload;
        if (kind == AcademicReportTypes.AssignmentGradeDistributionReport)
            return await BuildLearningGradePreviewAsync(request, payload, roots);
        if (kind == AcademicReportTypes.StudentwiseHomeworkSummaryReport)
            return await BuildLearningStudentSummaryPreviewAsync(request, payload, roots);
        List<LearningPreviewGroup> groups;
        if (kind == AcademicReportTypes.TeacherwiseHomeworkReport)
        {
            var schedules = await LoadLearningPreviewScheduleKeysAsync(request.Filters, false);
            groups = schedules.GroupBy(source => (source.Task.CreatorId, source.Task.SubjectId, source.Task.StandardId, source.DivisionId))
                .Select(group => new LearningPreviewGroup(group.First(), group.Select(source => source.Task).DistinctBy(task => task.Id).ToList(), group.ToList())).ToList();
        }
        else if (kind == AcademicReportTypes.SubjectwiseHomeworkReport)
            groups = roots.GroupBy(task => task.SubjectId).Select(group => new LearningPreviewGroup(new() { Task = group.First() }, group.ToList())).ToList();
        else if (kind == AcademicReportTypes.AssignmentPerformanceReport)
            groups = roots.GroupBy(task => (task.StandardId, task.SubjectId)).Select(group => new LearningPreviewGroup(new() { Task = group.First() }, group.ToList())).ToList();
        else groups = roots.Select(task => new LearningPreviewGroup(new() { Task = task }, [task])).ToList();
        await EnrichLearningExportAsync(groups.Select(group => group.Source).ToList(), CancellationToken.None);
        SetLearningPreviewPage(request, payload, groups.Count);
        var page = SelectLearningPreviewGroups(groups, request);
        if (page.Count == 0) return payload;
        var taskIds = page.SelectMany(group => group.Tasks).Select(task => task.Id).Distinct().ToArray();
        if (kind == AcademicReportTypes.SubjectwiseHomeworkReport)
            return await FillLearningSubjectSummaryPageAsync(request, payload, page, taskIds);
        if (kind == AcademicReportTypes.TeacherwiseHomeworkReport)
        {
            var stats = (await ReadLearningPreviewStatsAsync(request.Filters, false, taskIds,
                page.Select(group => group.Source.DivisionId).Distinct().ToArray(), byDivision: true)).ToDictionary(stat => (stat.TaskId, stat.DivisionId));
            var students = await ReadLearningPreviewStudentCountsAsync(page.Select(group => group.Source.DivisionId));
            foreach (var group in page)
            {
                var source = group.Source;
                var submitted = group.Tasks.Sum(task => stats.GetValueOrDefault((task.Id, source.DivisionId))?.Submitted ?? 0);
                var total = students.GetValueOrDefault(source.DivisionId);
                payload.Rows.Add(new()
                {
                    ["Teacher"] = source.Task.TeacherName, ["Subject"] = source.Task.SubjectName, ["Class"] = source.Task.StandardName,
                    ["Division"] = source.DivisionName, ["HomeworkAssigned"] = group.Tasks.Count.ToString(),
                    ["TotalStudents"] = total.ToString(),
                    ["SubmissionRate"] = $"{(total > 0 ? Math.Round((double)submitted / (total * group.Tasks.Count) * 100, 2) : 0)}%"
                });
            }
            return payload;
        }
        var rootStats = (await ReadLearningPreviewStatsAsync(request.Filters, assignments, taskIds)).ToDictionary(stat => stat.TaskId);
        if (kind == AcademicReportTypes.AssignmentEvaluationReport)
            return await FillLearningEvaluationPageAsync(request, payload, page, rootStats, taskIds);
        foreach (var group in page)
        {
            var source = group.Source;
            var values = group.Tasks.Select(task => rootStats.GetValueOrDefault(task.Id) ?? new LearningPreviewStats()).ToList();
            var count = values.Sum(stat => stat.Count);
            var submitted = values.Sum(stat => stat.Submitted);
            if (kind == AcademicReportTypes.HomeworkCompletionStatusReport)
            {
                var distinct = values.Single().DistinctStudents;
                payload.Rows.Add(new()
                {
                    ["HomeworkTitle"] = source.Task.Title, ["Subject"] = source.Task.SubjectName, ["AssignedStudents"] = distinct.ToString(),
                    ["Submitted"] = submitted.ToString(), ["Pending"] = Math.Max(0, distinct - submitted).ToString(),
                    ["CompletionPercent"] = LearningPercent(submitted, distinct)
                });
            }
            else
            {
                var marked = values.Sum(stat => stat.Marked);
                payload.Rows.Add(new()
                {
                    ["Class"] = source.Task.StandardName, ["Subject"] = source.Task.SubjectName, ["TotalAssignments"] = group.Tasks.Count.ToString(),
                    ["AverageScore"] = $"{(marked > 0 ? Math.Round(values.Sum(stat => stat.ScoreSum) / marked, 2) : 0)}%",
                    ["HighestScore"] = values.Where(stat => stat.Marked > 0).Select(stat => stat.Highest).DefaultIfEmpty().Max().ToString("F2"),
                    ["LowestScore"] = values.Where(stat => stat.Marked > 0).Select(stat => stat.Lowest).DefaultIfEmpty().Min().ToString("F2"),
                    ["CompletionRate"] = LearningPercent(submitted, count)
                });
            }
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> FillLearningSubjectSummaryPageAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload, List<LearningPreviewGroup> page, Guid[] taskIds)
    {
        await EnrichLearningExportAsync(page.SelectMany(group => group.Tasks).Select(task => new LearningExportSubmission { Task = task }).ToList(), CancellationToken.None);
        var stats = (await ReadLearningPreviewStatsAsync(request.Filters, false, taskIds)).ToDictionary(stat => stat.TaskId);
        var schedules = await LoadLearningPreviewScheduleKeysAsync(request.Filters, false, taskIds);
        var students = await ReadLearningPreviewStudentCountsAsync(schedules.Select(schedule => schedule.DivisionId));
        var assignedStudents = schedules.GroupBy(schedule => schedule.Task.Id)
            .ToDictionary(group => group.Key, group => group.Sum(schedule => students.GetValueOrDefault(schedule.DivisionId)));
        var subjects = page.Select(group => group.Source.Task.SubjectId).ToArray();
        var submissions = await GetLearningExportSubmissionsAsync(request.Filters, false);
        var distinct = await asyncExecuter.ToListAsync(submissions.Where(source => Enumerable.Contains(subjects, source.Task.SubjectId))
            .GroupBy(source => source.Task.SubjectId).Select(group => new { Id = group.Key, Count = group.Select(source => source.StudentId).Distinct().Count() }));
        var distinctBySubject = distinct.ToDictionary(row => row.Id, row => row.Count);
        foreach (var group in page)
        {
            var count = group.Tasks.Sum(task => stats.GetValueOrDefault(task.Id)?.Count ?? 0);
            var submitted = group.Tasks.Sum(task => stats.GetValueOrDefault(task.Id)?.Submitted ?? 0);
            var rates = group.Tasks.Select(task =>
            {
                var total = assignedStudents.GetValueOrDefault(task.Id);
                return total > 0 ? (double)(stats.GetValueOrDefault(task.Id)?.Submitted ?? 0) / total * 100 : 0;
            }).ToList();
            var names = group.Tasks.Select(task => task.TeacherSortName).Distinct().Where(name => !string.IsNullOrEmpty(name)).ToList();
            payload.Rows.Add(new()
            {
                ["Subject"] = group.Source.Task.SubjectName, ["StudentCount"] = distinctBySubject.GetValueOrDefault(group.Source.Task.SubjectId).ToString(),
                ["HomeworkCount"] = group.Tasks.Count.ToString(), ["AssignedTeacher"] = names.Count > 0 ? string.Join(", ", names) : "—",
                ["AverageSubmissionRate"] = $"{Math.Round(rates.Average(), 2)}%", ["CompletionRate"] = LearningPercent(submitted, count)
            });
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> FillLearningEvaluationPageAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload, List<LearningPreviewGroup> page, Dictionary<Guid, LearningPreviewStats> stats, Guid[] taskIds)
    {
        var source = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, true);
        var reviews = await asyncExecuter.ToListAsync(source.Where(submission => submission.Status == SubmissionStatus.Reviewed &&
                submission.AssignmentId.HasValue && Enumerable.Contains(taskIds, submission.AssignmentId.Value))
            .Select(submission => new LearningExportSubmission
            {
                Task = new LearningExportTask { Id = submission.AssignmentId!.Value },
                ReviewedBy = submission.ReviewedBy, ReviewedAt = submission.ReviewedAt
            }));
        await EnrichLearningExportAsync(reviews, CancellationToken.None);
        var byTask = reviews.ToLookup(review => review.Task.Id);
        foreach (var group in page)
        {
            var task = group.Source.Task;
            var names = byTask[task.Id].Where(review => review.ReviewedBy.HasValue).Select(review => review.ReviewerSortName)
                .Distinct().Where(name => !string.IsNullOrEmpty(name)).ToList();
            var latest = byTask[task.Id].Where(review => review.ReviewedAt.HasValue).Select(review => review.ReviewedAt!.Value).DefaultIfEmpty().Max();
            payload.Rows.Add(new()
            {
                ["Assignment"] = task.Title, ["EvaluatedBy"] = names.Count > 0 ? string.Join(", ", names) : "—",
                ["Subject"] = task.SubjectName, ["EvaluationDate"] = latest == default ? "—" : latest.ToString("yyyy-MM-dd"),
                ["StudentsEvaluated"] = (stats.GetValueOrDefault(task.Id)?.Evaluated ?? 0).ToString(),
                ["PendingEvaluations"] = (stats.GetValueOrDefault(task.Id)?.PendingEvaluation ?? 0).ToString(), ["Status"] = ((AssignmentStatus)task.Status).ToString()
            });
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningStudentSummaryPreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload, List<LearningExportTask> tasks)
    {
        var source = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, false);
        if (!await asyncExecuter.AnyAsync(source)) return payload;
        var students = await studentRepository.GetQueryableAsync();
        var encounters = await asyncExecuter.ToListAsync(source.Where(submission => students.Any(student => student.Id == submission.StudentId))
            .Select(submission => new LearningExportSubmission
            {
                Id = submission.Id, StudentId = submission.StudentId, DivisionId = submission.DivisionId,
                Task = new LearningExportTask { Id = submission.HomeworkAssignmentId!.Value }
            }));
        var byTask = tasks.ToDictionary(task => task.Id);
        for (var index = 0; index < encounters.Count; index++)
        {
            encounters[index].Task = byTask[encounters[index].Task.Id];
            encounters[index].SourceOrdinal = index;
        }
        var groups = encounters.GroupBy(encounter => LearningSummaryKey(encounter, request.ReportType))
            .Select(group => new LearningPreviewGroup(group.First(), [group.First().Task])).ToList();
        await EnrichLearningExportAsync(groups.Select(group => group.Source).ToList(), CancellationToken.None);
        SetLearningPreviewPage(request, payload, groups.Count);
        var page = SelectLearningPreviewGroups(groups, request);
        if (page.Count == 0) return payload;
        var studentIds = page.Select(group => group.Source.StudentId).Distinct().ToArray();
        var subjectIds = page.Select(group => group.Source.Task.SubjectId).Distinct().ToArray();
        var submissions = await GetLearningExportSubmissionsAsync(request.Filters, false);
        var today = DateTime.UtcNow.Date;
        var counts = await asyncExecuter.ToListAsync(submissions.Where(submission => Enumerable.Contains(studentIds, submission.StudentId) &&
                Enumerable.Contains(subjectIds, submission.Task.SubjectId))
            .GroupBy(submission => new { submission.StudentId, submission.Task.SubjectId }).Select(group => new
            {
                group.Key.StudentId, group.Key.SubjectId, Count = group.Count(),
                Submitted = group.Count(submission => submission.Status == SubmissionStatus.Submitted || submission.Status == SubmissionStatus.Late ||
                    submission.Status == SubmissionStatus.Reviewed || submission.Status == SubmissionStatus.Excused || submission.Status == SubmissionStatus.NeedsReview),
                Pending = group.Count(submission => (submission.Status == SubmissionStatus.NotSubmitted || submission.Status == SubmissionStatus.Draft) && submission.DueDate.Date >= today),
                Overdue = group.Count(submission => (submission.Status == SubmissionStatus.NotSubmitted || submission.Status == SubmissionStatus.Draft) && submission.DueDate.Date < today)
            }));
        var byKey = counts.ToDictionary(row => (row.StudentId, row.SubjectId));
        foreach (var group in page)
        {
            var first = group.Source;
            var stat = byKey[(first.StudentId, first.Task.SubjectId)];
            payload.Rows.Add(new()
            {
                ["StudentName"] = first.StudentName, ["Class"] = first.Task.StandardName, ["Division"] = first.DivisionName,
                ["Subject"] = first.Task.SubjectName, ["AssignedHomework"] = stat.Count.ToString(), ["Submitted"] = stat.Submitted.ToString(),
                ["Pending"] = stat.Pending.ToString(), ["Overdue"] = stat.Overdue.ToString(), ["CompletionPercent"] = LearningPercent(stat.Submitted, stat.Count)
            });
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningGradePreviewAsync(AcademicReportRequestDto request,
        AcademicReportPreviewDto payload, List<LearningExportTask> tasks)
    {
        var source = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, true);
        var encounters = await asyncExecuter.ToListAsync(source.Where(submission => submission.Marks.HasValue).Select(submission => submission.AssignmentId!.Value));
        var taskOrder = encounters.Distinct().ToList();
        var submissions = await GetLearningExportSubmissionsAsync(request.Filters, true);
        var scores = submissions.Where(submission => submission.Marks.HasValue).Select(submission => new
        {
            TaskId = submission.Task.Id, Obtained = (double)submission.Marks!.Value,
            Total = (double)(submission.TotalMarks ?? submission.Task.TotalMarks)
        });
        var grades = scores.Select(score => new
        {
            score.TaskId, score.Obtained,
            Grade = score.Total <= 0 ? "—" : score.Obtained / score.Total * 100 >= 90 ? "A+" : score.Obtained / score.Total * 100 >= 80 ? "A" :
                score.Obtained / score.Total * 100 >= 70 ? "B+" : score.Obtained / score.Total * 100 >= 60 ? "B" :
                score.Obtained / score.Total * 100 >= 50 ? "C+" : score.Obtained / score.Total * 100 >= 40 ? "C" : score.Obtained / score.Total * 100 >= 33 ? "D" : "F"
        });
        var counts = await asyncExecuter.ToListAsync(grades.GroupBy(grade => new { grade.TaskId, grade.Grade }).Select(group => new
        {
            group.Key.TaskId, group.Key.Grade, Count = group.Count(), Highest = group.Max(grade => grade.Obtained), Lowest = group.Min(grade => grade.Obtained)
        }));
        var byKey = counts.ToDictionary(row => (row.TaskId, row.Grade));
        var totals = counts.GroupBy(row => row.TaskId).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
        var byTask = tasks.ToDictionary(task => task.Id);
        var groups = new List<LearningPreviewGroup>();
        foreach (var taskId in taskOrder)
            foreach (var grade in new[] { "A+", "A", "B+", "B", "C+", "C", "D", "F" })
                if (byKey.ContainsKey((taskId, grade))) groups.Add(new(new() { Task = byTask[taskId], SourceOrdinal = groups.Count }, [byTask[taskId]], Grade: grade));
        SetLearningPreviewPage(request, payload, groups.Count);
        foreach (var group in SelectLearningPreviewGroups(groups, request))
        {
            var task = group.Source.Task;
            var stat = byKey[(task.Id, group.Grade!)];
            payload.Rows.Add(new()
            {
                ["Grade"] = group.Grade, ["Assignment"] = task.Title, ["StudentCount"] = stat.Count.ToString(),
                ["Percentage"] = LearningPercent(stat.Count, totals[task.Id]), ["HighestMarks"] = stat.Highest.ToString("F2"), ["LowestMarks"] = stat.Lowest.ToString("F2")
            });
        }
        return payload;
    }
}
