using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private async Task<AcademicReportPreviewDto?> TryBuildLearningTaskPreviewAsync(
        AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var kind = request.ReportType;
        var schedule = kind is AcademicReportTypes.DailyHomeworkReport or AcademicReportTypes.ClasswiseHomeworkReport
            or AcademicReportTypes.AssignmentIssuanceReport;
        var detail = kind is AcademicReportTypes.PendingHomeworkReport or AcademicReportTypes.OverdueHomeworkReport
            or AcademicReportTypes.HomeworkSubmissionHistoryReport or AcademicReportTypes.HomeworkAcknowledgementReport
            or AcademicReportTypes.AssignmentSubmissionReport or AcademicReportTypes.AssignmentMarksReport
            or AcademicReportTypes.LateSubmissionReport or AcademicReportTypes.MissingAssignmentReport
            or AcademicReportTypes.AssignmentFeedbackReport;
        var summary = kind is AcademicReportTypes.SubjectwiseHomeworkReport or AcademicReportTypes.TeacherwiseHomeworkReport
            or AcademicReportTypes.StudentwiseHomeworkSummaryReport or AcademicReportTypes.HomeworkCompletionStatusReport
            or AcademicReportTypes.AssignmentEvaluationReport or AcademicReportTypes.AssignmentGradeDistributionReport
            or AcademicReportTypes.AssignmentPerformanceReport;
        if (kind is AcademicReportTypes.HomeworkLoadAnalysisReport or AcademicReportTypes.AssignmentLoadAnalysisReport
            or AcademicReportTypes.TeacherPerformanceInHomeworkEvaluationReport or AcademicReportTypes.StudentPerformanceTrendReport
            or AcademicReportTypes.ClasswiseOverallHomeworkAssignmentReport)
        {
            var metadata = await TryCreateLearningAnalyticsExportAsync(request, request.PageSize, CancellationToken.None);
            payload.Columns = metadata!.Metadata.Columns;
            return await BuildLearningAnalyticsPreviewAsync(request, payload);
        }
        if (!schedule && !detail && !summary && kind != AcademicReportTypes.HomeworkWithAttachmentsReport) return null;
        payload.Columns = LearningTaskExportColumns(kind)!;
        if (summary) return await BuildLearningSummaryPreviewAsync(request, payload);
        if (schedule) return await BuildLearningSchedulePreviewAsync(request, payload);
        if (kind == AcademicReportTypes.HomeworkWithAttachmentsReport) return await BuildLearningAttachmentPreviewAsync(request, payload);
        return await BuildLearningSubmissionPreviewAsync(request, payload);
    }

    private static IQueryable<LearningExportTask> ProjectLearningPreviewTasks(IQueryable<LearningExportTask> tasks) =>
        tasks.Select(task => new LearningExportTask
        {
            Id = task.Id, StandardId = task.StandardId, SubjectId = task.SubjectId, CreatorId = task.CreatorId,
            CreationTime = task.CreationTime, Title = task.Title, TotalMarks = task.TotalMarks, Status = task.Status
        });

    private static List<LearningExportSubmission> SelectLearningPreviewPage(IEnumerable<LearningExportSubmission> sources,
        AcademicReportRequestDto request) => sources.Select(source => new LearningExportRow(source, []))
        .OrderBy(row => row, Comparer<LearningExportRow>.Create((a, b) => request.ReportType == AcademicReportTypes.TeacherwiseHomeworkReport
            ? Comparer<string?>.Default.Compare(a.Source.Task.TeacherSortName, b.Source.Task.TeacherSortName)
            : CompareLearningExportRows(a, b, request.ReportType)))
        .Skip(PreviewSkip(request)).Take(request.PageSize).Select(row => row.Source).ToList();

    private static void SetLearningPreviewPage(AcademicReportRequestDto request, AcademicReportPreviewDto payload, int count)
    {
        payload.TotalCount = count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;
    }

    private async Task<IQueryable<StudentSubmissionEntity>> GetLearningPreviewSubmissionEntitiesAsync(
        AcademicReportFilterDto filters, bool assignments)
    {
        var tasks = await GetLearningExportTasksAsync(filters, assignments);
        var ids = tasks.Select(task => task.Id);
        var submissions = await submissionRepository.GetQueryableAsync();
        submissions = assignments
            ? submissions.Where(submission => submission.AssignmentId.HasValue && ids.Contains(submission.AssignmentId.Value))
            : submissions.Where(submission => submission.HomeworkAssignmentId.HasValue && ids.Contains(submission.HomeworkAssignmentId.Value));
        if (filters.DivisionIds is { Count: > 0 }) submissions = submissions.Where(submission => filters.DivisionIds.Contains(submission.DivisionId));
        if (filters.StudentIds is { Count: > 0 }) submissions = submissions.Where(submission => filters.StudentIds.Contains(submission.StudentId));
        return submissions;
    }

    private async Task HydrateLearningPreviewPageAsync(IQueryable<LearningExportTask> tasks,
        List<LearningExportSubmission> page)
    {
        if (page.Count == 0) return;
        var ids = page.Select(source => source.Task.Id).Distinct().ToArray();
        var roots = await asyncExecuter.ToListAsync(ProjectLearningPreviewTasks(tasks.Where(task => Enumerable.Contains(ids, task.Id))));
        var byId = roots.ToDictionary(task => task.Id);
        foreach (var source in page) source.Task = byId[source.Task.Id];
        await EnrichLearningExportAsync(page, CancellationToken.None);
    }

    private async Task<AcademicReportPreviewDto> BuildLearningSchedulePreviewAsync(
        AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var assignments = request.ReportType == AcademicReportTypes.AssignmentIssuanceReport;
        var tasks = await GetLearningExportTasksAsync(request.Filters, assignments);
        if (!await asyncExecuter.AnyAsync(tasks)) return payload;
        var keys = await LoadLearningPreviewScheduleKeysAsync(request.Filters, assignments);
        SetLearningPreviewPage(request, payload, keys.Count);
        var page = SelectLearningPreviewPage(keys, request);
        if (page.Count == 0) return payload;
        await EnrichLearningExportAsync(page, CancellationToken.None);
        var taskIds = page.Select(source => source.Task.Id).Distinct().ToArray();
        var divisionIds = page.Select(source => source.DivisionId).Distinct().ToArray();
        var submissions = await GetLearningExportSubmissionsAsync(request.Filters, assignments);
        var counts = await asyncExecuter.ToListAsync(submissions.Where(source => Enumerable.Contains(taskIds, source.Task.Id) &&
                Enumerable.Contains(divisionIds, source.DivisionId))
            .GroupBy(source => new { TaskId = source.Task.Id, source.DivisionId }).Select(group => new
            {
                group.Key.TaskId, group.Key.DivisionId,
                Submitted = group.Count(source => source.Status == SubmissionStatus.Submitted || source.Status == SubmissionStatus.Late ||
                    source.Status == SubmissionStatus.Reviewed || source.Status == SubmissionStatus.Excused || source.Status == SubmissionStatus.NeedsReview)
            }));
        var countsByPair = counts.ToDictionary(row => (row.TaskId, row.DivisionId), row => row.Submitted);
        var students = await studentRepository.GetQueryableAsync();
        var studentCounts = await asyncExecuter.ToListAsync(students.Where(student => student.DivisionId.HasValue &&
                Enumerable.Contains(divisionIds, student.DivisionId.Value))
            .GroupBy(student => student.DivisionId!.Value).Select(group => new { Id = group.Key, Count = group.Count() }));
        var studentsByDivision = studentCounts.ToDictionary(row => row.Id, row => row.Count);
        foreach (var source in page)
        {
            var task = source.Task;
            var total = studentsByDivision.GetValueOrDefault(source.DivisionId);
            var submitted = countsByPair.GetValueOrDefault((task.Id, source.DivisionId));
            payload.Rows.Add(request.ReportType switch
            {
                AcademicReportTypes.ClasswiseHomeworkReport => new()
                {
                    ["Class"] = task.StandardName, ["Subject"] = task.SubjectName, ["HomeworkTitle"] = task.Title,
                    ["AssignedDate"] = task.CreationTime.ToString("yyyy-MM-dd"), ["DueDate"] = source.DueDate.ToString("yyyy-MM-dd"),
                    ["Teacher"] = task.TeacherName, ["SubmissionRate"] = LearningPercent(submitted, total)
                },
                AcademicReportTypes.AssignmentIssuanceReport => new()
                {
                    ["AssignmentTitle"] = task.Title, ["Subject"] = task.SubjectName, ["Class"] = task.StandardName,
                    ["Division"] = source.DivisionName, ["AssignedBy"] = task.TeacherName,
                    ["AssignedDate"] = task.CreationTime.ToString("yyyy-MM-dd"),
                    ["DueDate"] = source.DueDate == DateTime.MaxValue ? "—" : source.DueDate.ToString("yyyy-MM-dd"),
                    ["TotalStudents"] = total.ToString(), ["Status"] = ((AssignmentStatus)task.Status).ToString()
                },
                _ => new()
                {
                    ["HomeworkDate"] = task.CreationTime.ToString("yyyy-MM-dd"), ["Class"] = task.StandardName,
                    ["Division"] = source.DivisionName, ["Subject"] = task.SubjectName, ["HomeworkTitle"] = task.Title,
                    ["AssignedBy"] = task.TeacherName, ["DueDate"] = source.DueDate.ToString("yyyy-MM-dd"),
                    ["TotalStudents"] = total.ToString(), ["SubmissionCount"] = submitted.ToString(), ["Status"] = ((HomeworkStatus)task.Status).ToString()
                }
            });
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningAttachmentPreviewAsync(
        AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var tasks = await GetLearningExportTasksAsync(request.Filters, false);
        if (!await asyncExecuter.AnyAsync(tasks)) return payload;
        var attached = tasks.Where(task => !string.IsNullOrWhiteSpace(task.AttachmentsJson) || !string.IsNullOrWhiteSpace(task.DocumentUrl));
        var keys = await asyncExecuter.ToListAsync(ProjectLearningPreviewTasks(attached).OrderBy(task => task.Id));
        SetLearningPreviewPage(request, payload, keys.Count);
        var page = SelectLearningPreviewPage(keys.Select(task => new LearningExportSubmission { Task = task }), request);
        if (page.Count == 0) return payload;
        await EnrichLearningExportAsync(page, CancellationToken.None);
        var ids = page.Select(source => source.Task.Id).ToArray();
        var content = await asyncExecuter.ToListAsync(tasks.Where(task => Enumerable.Contains(ids, task.Id))
            .Select(task => new { task.Id, task.AttachmentsJson }).Take(request.PageSize));
        var attachments = content.ToDictionary(task => task.Id, task => task.AttachmentsJson);
        foreach (var source in page)
        {
            var count = 1;
            if (!string.IsNullOrWhiteSpace(attachments[source.Task.Id]))
            {
                try { count = JsonSerializer.Deserialize<JsonElement[]>(attachments[source.Task.Id]!)?.Length ?? 1; }
                catch (JsonException) { count = 1; }
            }
            payload.Rows.Add(new()
            {
                ["HomeworkTitle"] = source.Task.Title, ["Subject"] = source.Task.SubjectName, ["Teacher"] = source.Task.TeacherName,
                ["AttachmentCount"] = count.ToString(), ["AttachmentType"] = "—", ["AssignedDate"] = source.Task.CreationTime.ToString("yyyy-MM-dd")
            });
        }
        return payload;
    }

    private async Task<AcademicReportPreviewDto> BuildLearningSubmissionPreviewAsync(
        AcademicReportRequestDto request, AcademicReportPreviewDto payload)
    {
        var kind = request.ReportType;
        var assignments = kind is AcademicReportTypes.AssignmentSubmissionReport or AcademicReportTypes.AssignmentMarksReport
            or AcademicReportTypes.LateSubmissionReport or AcademicReportTypes.MissingAssignmentReport or AcademicReportTypes.AssignmentFeedbackReport;
        var source = await GetLearningPreviewSubmissionEntitiesAsync(request.Filters, assignments);
        if (!await asyncExecuter.AnyAsync(source)) return payload;
        if (kind is AcademicReportTypes.PendingHomeworkReport or AcademicReportTypes.OverdueHomeworkReport or AcademicReportTypes.MissingAssignmentReport)
            source = source.Where(submission => submission.Status == SubmissionStatus.NotSubmitted || submission.Status == SubmissionStatus.Draft);
        var today = DateTime.UtcNow.Date;
        if (kind == AcademicReportTypes.OverdueHomeworkReport) source = source.Where(submission => submission.DueDate.Date < today);
        if (kind == AcademicReportTypes.AssignmentMarksReport) source = source.Where(submission => submission.Marks.HasValue);
        if (kind == AcademicReportTypes.AssignmentFeedbackReport)
            source = source.Where(LearningPreviewHasFeedback);
        if (kind == AcademicReportTypes.LateSubmissionReport)
            source = source.Where(submission => submission.Status == SubmissionStatus.Late || submission.SubmissionDate.HasValue);
        var students = await studentRepository.GetQueryableAsync();
        source = source.Where(submission => students.Any(student => student.Id == submission.StudentId));
        var keys = await asyncExecuter.ToListAsync(source.Select(submission => new LearningExportSubmission
        {
            Id = submission.Id, StudentId = submission.StudentId, DivisionId = submission.DivisionId,
            Task = new LearningExportTask { Id = assignments ? submission.AssignmentId!.Value : submission.HomeworkAssignmentId!.Value },
            DueDate = submission.DueDate, SubmissionDate = submission.SubmissionDate, Status = submission.Status,
            Marks = submission.Marks, TotalMarks = submission.TotalMarks, ReviewedBy = submission.ReviewedBy, ReviewedAt = submission.ReviewedAt
        }));
        if (kind == AcademicReportTypes.LateSubmissionReport)
            keys.RemoveAll(submission => submission.Status != SubmissionStatus.Late && LearningExportDelay(submission) <= 0);
        for (var index = 0; index < keys.Count; index++) keys[index].SourceOrdinal = index;
        if (kind is AcademicReportTypes.HomeworkAcknowledgementReport or AcademicReportTypes.AssignmentSubmissionReport or AcademicReportTypes.AssignmentMarksReport)
            await EnrichLearningExportAsync(keys, CancellationToken.None);
        SetLearningPreviewPage(request, payload, keys.Count);
        var page = SelectLearningPreviewPage(keys, request);
        if (page.Count == 0) return payload;
        await HydrateLearningPreviewPageAsync(await GetLearningExportTasksAsync(request.Filters, assignments), page);
        if (kind == AcademicReportTypes.AssignmentFeedbackReport)
        {
            var ids = page.Select(submission => submission.Id).ToArray();
            var content = await asyncExecuter.ToListAsync(source.Where(submission => Enumerable.Contains(ids, submission.Id))
                .Select(submission => new { submission.Id, submission.Feedback, submission.Remarks }).Take(request.PageSize));
            var byId = content.ToDictionary(submission => submission.Id);
            foreach (var submission in page)
            {
                submission.Feedback = byId[submission.Id].Feedback;
                submission.Remarks = byId[submission.Id].Remarks;
            }
        }
        foreach (var submission in page) payload.Rows.Add(FormatLearningPreviewSubmission(submission, kind, today));
        return payload;
    }

    private static Dictionary<string, string?> FormatLearningPreviewSubmission(LearningExportSubmission source, string kind, DateTime today)
    {
        var task = source.Task;
        var due = source.DueDate.ToString("yyyy-MM-dd");
        var submitted = source.SubmissionDate?.ToString("yyyy-MM-dd") ?? "—";
        return kind switch
        {
            AcademicReportTypes.PendingHomeworkReport => new()
            {
                ["Student"] = source.StudentName, ["Class"] = task.StandardName, ["Division"] = source.DivisionName,
                ["Subject"] = task.SubjectName, ["Homework"] = task.Title, ["DueDate"] = due,
                ["DaysPending"] = Math.Max(0, (today - source.DueDate.Date).Days).ToString()
            },
            AcademicReportTypes.OverdueHomeworkReport => new()
            {
                ["Student"] = source.StudentName, ["Homework"] = task.Title, ["Subject"] = task.SubjectName,
                ["DueDate"] = due, ["OverdueDays"] = (today - source.DueDate.Date).Days.ToString()
            },
            AcademicReportTypes.HomeworkSubmissionHistoryReport => new()
            {
                ["SubmissionDate"] = submitted, ["Student"] = source.StudentName, ["Homework"] = task.Title,
                ["Subject"] = task.SubjectName, ["SubmittedOnTime"] = source.SubmissionDate.HasValue
                    ? source.SubmissionDate.Value.UtcDateTime.Date <= source.DueDate.Date ? "Yes" : "No" : "—",
                ["SubmissionStatus"] = source.Status.ToString()
            },
            AcademicReportTypes.HomeworkAcknowledgementReport => new()
            { ["Homework"] = task.Title, ["Student"] = source.StudentName, ["AcknowledgedBy"] = "—", ["AcknowledgementDate"] = "—", ["Status"] = "—" },
            AcademicReportTypes.AssignmentSubmissionReport => new()
            { ["Assignment"] = task.Title, ["Student"] = source.StudentName, ["Subject"] = task.SubjectName, ["SubmissionDate"] = submitted, ["SubmissionStatus"] = source.Status.ToString(), ["DueDate"] = due },
            AcademicReportTypes.AssignmentMarksReport => FormatLearningPreviewMarks(source),
            AcademicReportTypes.LateSubmissionReport => new()
            { ["Student"] = source.StudentName, ["Assignment"] = task.Title, ["DueDate"] = due, ["SubmissionDate"] = submitted, ["DelayDays"] = Math.Max(0, LearningExportDelay(source)).ToString(), ["Subject"] = task.SubjectName },
            AcademicReportTypes.MissingAssignmentReport => new()
            { ["Student"] = source.StudentName, ["Assignment"] = task.Title, ["Subject"] = task.SubjectName, ["DueDate"] = due, ["PendingDays"] = Math.Max(0, (today - source.DueDate.Date).Days).ToString(), ["Teacher"] = task.TeacherName },
            AcademicReportTypes.AssignmentFeedbackReport => new()
            {
                ["Student"] = source.StudentName, ["Subject"] = task.SubjectName, ["Assignment"] = task.Title,
                ["Teacher"] = source.ReviewerName, ["FeedbackDate"] = source.ReviewedAt?.ToString("yyyy-MM-dd") ?? "—",
                ["FeedbackRemarks"] = !string.IsNullOrWhiteSpace(source.Feedback) ? source.Feedback : source.Remarks, ["Rating"] = "—"
            },
            _ => throw new InvalidOperationException($"Unsupported learning detail preview: {kind}")
        };
    }

    private static Dictionary<string, string?> FormatLearningPreviewMarks(LearningExportSubmission source)
    {
        var obtained = (double)source.Marks!.Value;
        var total = (double)(source.TotalMarks ?? source.Task.TotalMarks);
        return new()
        {
            ["Student"] = source.StudentName, ["Assignment"] = source.Task.Title, ["Subject"] = source.Task.SubjectName,
            ["ObtainedMarks"] = obtained.ToString("F2"), ["TotalMarks"] = total.ToString("F0"),
            ["Percentage"] = $"{(total > 0 ? Math.Round(obtained / total * 100, 2) : 0)}%", ["Grade"] = ComputeGrade(obtained, total)
        };
    }

    private static readonly Expression<Func<StudentSubmissionEntity, bool>> LearningPreviewHasFeedback =
        BuildLearningPreviewFeedbackPredicate();

    private static Expression<Func<StudentSubmissionEntity, bool>> BuildLearningPreviewFeedbackPredicate()
    {
        var submission = Expression.Parameter(typeof(StudentSubmissionEntity), "submission");
        var replace = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
        Expression HasText(string property)
        {
            Expression text = Expression.Coalesce(Expression.Property(submission, property), Expression.Constant(string.Empty));
            // SQLite's trim removes spaces only; replace .NET whitespace inside SQL before testing presence.
            foreach (var character in Enumerable.Range(char.MinValue, char.MaxValue + 1).Select(value => (char)value).Where(char.IsWhiteSpace))
                text = Expression.Call(text, replace, Expression.Constant(character.ToString()), Expression.Constant(string.Empty));
            return Expression.NotEqual(text, Expression.Constant(string.Empty));
        }
        return Expression.Lambda<Func<StudentSubmissionEntity, bool>>(
            Expression.OrElse(HasText(nameof(StudentSubmissionEntity.Feedback)), HasText(nameof(StudentSubmissionEntity.Remarks))), submission);
    }
}
