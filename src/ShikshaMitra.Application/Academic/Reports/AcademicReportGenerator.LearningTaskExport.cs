using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportGenerator
{
    private Task<AcademicReportExportRows?> TryCreateLearningTaskExportAsync(AcademicReportRequestDto request,
        CancellationToken cancellationToken, int batchSize)
    {
        var columns = LearningTaskExportColumns(request.ReportType);
        if (columns == null) return Task.FromResult<AcademicReportExportRows?>(null);
        var metadata = new AcademicReportPreviewDto
        {
            ReportType = request.ReportType, GeneratedAtUtc = DateTime.UtcNow,
            FilterSummary = BuildFilterSummary(request.Filters), NormalizedFilters = request.Filters,
            Columns = columns, Page = 1
        };
        return Task.FromResult<AcademicReportExportRows?>(new(metadata,
            ReadLearningTaskExportRowsAsync(request, batchSize, cancellationToken)));
    }

    private async Task<IQueryable<LearningExportTask>> GetLearningExportTasksAsync(AcademicReportFilterDto filters,
        bool assignments)
    {
        IQueryable<LearningExportTask> query;
        if (assignments)
        {
            var roots = await assignmentRepository.GetQueryableAsync();
            if (filters.AssignmentIds is { Count: > 0 }) roots = roots.Where(a => filters.AssignmentIds.Contains(a.Id));
            if (filters.DivisionIds is { Count: > 0 }) roots = roots.Where(a => filters.DivisionIds.Contains(a.DivisionId)
                || a.DivisionSchedules.Any(s => filters.DivisionIds.Contains(s.DivisionId)));
            query = roots.Select(a => new LearningExportTask
            {
                Id = a.Id, StandardId = a.StandardId, SubjectId = a.SubjectId, CreatorId = a.CreatorId,
                CreationTime = a.CreationTime, Title = a.Title, TotalMarks = a.TotalMarks,
                Status = (int)a.Status, AttachmentsJson = a.AttachmentsJson, DocumentUrl = null
            });
        }
        else
        {
            var roots = await homeworkRepository.GetQueryableAsync();
            if (filters.DivisionIds is { Count: > 0 }) roots = roots.Where(a =>
                (a.DivisionId.HasValue && filters.DivisionIds.Contains(a.DivisionId.Value))
                || a.DivisionSchedules.Any(s => filters.DivisionIds.Contains(s.DivisionId)));
            query = roots.Select(a => new LearningExportTask
            {
                Id = a.Id, StandardId = a.StandardId, SubjectId = a.SubjectId, CreatorId = a.CreatorId,
                CreationTime = a.CreationTime, Title = a.Title, TotalMarks = 0,
                Status = (int)a.Status, AttachmentsJson = a.AttachmentsJson, DocumentUrl = a.DocumentUrl
            });
        }
        if (filters.FromDate.HasValue) query = query.Where(a => a.CreationTime >= filters.FromDate.Value);
        if (filters.ToDate.HasValue) query = query.Where(a => a.CreationTime < filters.ToDate.Value.AddDays(1));
        if (filters.ClassIds is { Count: > 0 }) query = query.Where(a => filters.ClassIds.Contains(a.StandardId));
        if (filters.SubjectIds is { Count: > 0 }) query = query.Where(a => filters.SubjectIds.Contains(a.SubjectId));
        if (filters.TeacherIds is { Count: > 0 })
        {
            var teachers = await employeeRepository.GetQueryableAsync();
            var users = teachers.Where(e => filters.TeacherIds.Contains(e.Id) && e.UserId.HasValue).Select(e => e.UserId!.Value);
            query = query.Where(a => a.CreatorId.HasValue && users.Contains(a.CreatorId.Value));
        }
        return query;
    }

    private async Task<IQueryable<LearningExportSubmission>> GetLearningExportSubmissionsAsync(
        AcademicReportFilterDto filters, bool assignments)
    {
        var tasks = await GetLearningExportTasksAsync(filters, assignments);
        var submissions = await submissionRepository.GetQueryableAsync();
        if (filters.DivisionIds is { Count: > 0 }) submissions = submissions.Where(s => filters.DivisionIds.Contains(s.DivisionId));
        if (filters.StudentIds is { Count: > 0 }) submissions = submissions.Where(s => filters.StudentIds.Contains(s.StudentId));
        return from task in tasks
               join submission in submissions on (Guid?)task.Id equals (assignments ? submission.AssignmentId : submission.HomeworkAssignmentId)
               select new LearningExportSubmission
               {
                   Task = task, Id = submission.Id, StudentId = submission.StudentId, DivisionId = submission.DivisionId,
                   DueDate = submission.DueDate, SubmissionDate = submission.SubmissionDate, Status = submission.Status,
                   Marks = submission.Marks, TotalMarks = submission.TotalMarks, ReviewedBy = submission.ReviewedBy,
                   ReviewedAt = submission.ReviewedAt, Feedback = submission.Feedback, Remarks = submission.Remarks
               };
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> ReadLearningTaskExportRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = request.ReportType switch
        {
            AcademicReportTypes.DailyHomeworkReport or AcademicReportTypes.ClasswiseHomeworkReport or AcademicReportTypes.AssignmentIssuanceReport =>
                BuildLearningScheduleRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.HomeworkCompletionStatusReport => BuildHomeworkCompletionRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.TeacherwiseHomeworkReport => BuildTeacherwiseHomeworkRowsAsync(request, batchSize, cancellationToken),
            AcademicReportTypes.SubjectwiseHomeworkReport or AcademicReportTypes.StudentwiseHomeworkSummaryReport or AcademicReportTypes.AssignmentEvaluationReport
                or AcademicReportTypes.AssignmentGradeDistributionReport or AcademicReportTypes.AssignmentPerformanceReport => BuildLearningSummaryRowsAsync(request, batchSize, cancellationToken),
            _ => BuildLearningTaskDetailRowsAsync(request, batchSize, cancellationToken)
        };
        await foreach (var row in AcademicReportExternalSort.SortAsync(rows,
            (a, b) => CompareLearningExportRows(a, b, request.ReportType), cancellationToken: cancellationToken))
            yield return row.Values;
    }

    private async IAsyncEnumerable<LearningExportRow> BuildLearningTaskDetailRowsAsync(
        AcademicReportRequestDto request, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kind = request.ReportType;
        var assignments = kind is AcademicReportTypes.AssignmentSubmissionReport or AcademicReportTypes.AssignmentMarksReport
            or AcademicReportTypes.LateSubmissionReport or AcademicReportTypes.MissingAssignmentReport or AcademicReportTypes.AssignmentFeedbackReport;
        if (kind == AcademicReportTypes.HomeworkWithAttachmentsReport)
        {
            var tasks = await GetLearningExportTasksAsync(request.Filters, false);
            for (var offset = 0; ; offset += batchSize)
            {
                var batch = await asyncExecuter.ToListAsync(tasks.OrderBy(t => t.Id).Skip(offset).Take(batchSize), cancellationToken);
                if (batch.Count == 0) yield break;
                var facts = batch.Select(t => new LearningExportSubmission { Task = t }).ToList();
                await EnrichLearningExportAsync(facts, cancellationToken);
                foreach (var fact in facts)
                {
                    var task = fact.Task;
                    if (string.IsNullOrWhiteSpace(task.AttachmentsJson) && string.IsNullOrWhiteSpace(task.DocumentUrl)) continue;
                    var count = 1;
                    if (!string.IsNullOrWhiteSpace(task.AttachmentsJson))
                    {
                        try { count = JsonSerializer.Deserialize<JsonElement[]>(task.AttachmentsJson)?.Length ?? 1; }
                        catch (JsonException) { count = 1; }
                    }
                    yield return new(fact, new()
                    {
                        ["HomeworkTitle"] = task.Title, ["Subject"] = task.SubjectName, ["Teacher"] = task.TeacherName,
                        ["AttachmentCount"] = count.ToString(), ["AttachmentType"] = "—", ["AssignedDate"] = task.CreationTime.ToString("yyyy-MM-dd")
                    });
                }
                if (batch.Count < batchSize) yield break;
            }
        }
        var today = DateTime.UtcNow.Date;
        await foreach (var batch in ReadLearningSubmissionBatchesAsync(request.Filters, assignments, batchSize, cancellationToken))
        {
            foreach (var fact in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (fact.StudentName == null) continue;
                if (kind is AcademicReportTypes.PendingHomeworkReport or AcademicReportTypes.OverdueHomeworkReport or AcademicReportTypes.MissingAssignmentReport &&
                    fact.Status is not (SubmissionStatus.NotSubmitted or SubmissionStatus.Draft)) continue;
                if (kind == AcademicReportTypes.AssignmentMarksReport && !fact.Marks.HasValue) continue;
                var task = fact.Task;
                var due = fact.DueDate.ToString("yyyy-MM-dd");
                var submittedOn = fact.SubmissionDate?.ToString("yyyy-MM-dd") ?? "—";
                Dictionary<string, string?> values;
                switch (kind)
                {
                    case AcademicReportTypes.PendingHomeworkReport:
                        values = new() { ["Student"] = fact.StudentName, ["Class"] = task.StandardName, ["Division"] = fact.DivisionName,
                            ["Subject"] = task.SubjectName, ["Homework"] = task.Title, ["DueDate"] = due,
                            ["DaysPending"] = Math.Max(0, (today - fact.DueDate.Date).Days).ToString() };
                        break;
                    case AcademicReportTypes.OverdueHomeworkReport:
                        if (fact.DueDate.Date >= today) continue;
                        values = new() { ["Student"] = fact.StudentName, ["Homework"] = task.Title, ["Subject"] = task.SubjectName,
                            ["DueDate"] = due, ["OverdueDays"] = (today - fact.DueDate.Date).Days.ToString() };
                        break;
                    case AcademicReportTypes.HomeworkSubmissionHistoryReport:
                        values = new() { ["SubmissionDate"] = submittedOn, ["Student"] = fact.StudentName, ["Homework"] = task.Title,
                            ["Subject"] = task.SubjectName, ["SubmittedOnTime"] = fact.SubmissionDate.HasValue
                                ? (fact.SubmissionDate.Value.UtcDateTime.Date <= fact.DueDate.Date ? "Yes" : "No") : "—",
                            ["SubmissionStatus"] = fact.Status.ToString() };
                        break;
                    case AcademicReportTypes.HomeworkAcknowledgementReport:
                        values = new() { ["Homework"] = task.Title, ["Student"] = fact.StudentName, ["AcknowledgedBy"] = "—",
                            ["AcknowledgementDate"] = "—", ["Status"] = "—" };
                        break;
                    case AcademicReportTypes.AssignmentSubmissionReport:
                        values = new() { ["Assignment"] = task.Title, ["Student"] = fact.StudentName, ["Subject"] = task.SubjectName,
                            ["SubmissionDate"] = submittedOn, ["SubmissionStatus"] = fact.Status.ToString(), ["DueDate"] = due };
                        break;
                    case AcademicReportTypes.AssignmentMarksReport:
                        var obtained = (double)fact.Marks!.Value;
                        var total = (double)(fact.TotalMarks ?? task.TotalMarks);
                        values = new() { ["Student"] = fact.StudentName, ["Assignment"] = task.Title, ["Subject"] = task.SubjectName,
                            ["ObtainedMarks"] = obtained.ToString("F2"), ["TotalMarks"] = total.ToString("F0"),
                            ["Percentage"] = $"{(total > 0 ? Math.Round(obtained / total * 100, 2) : 0)}%", ["Grade"] = ComputeGrade(obtained, total) };
                        break;
                    case AcademicReportTypes.LateSubmissionReport:
                        if (fact.Status != SubmissionStatus.Late && !(fact.SubmissionDate.HasValue && fact.SubmissionDate.Value.UtcDateTime.Date > fact.DueDate.Date)) continue;
                        values = new() { ["Student"] = fact.StudentName, ["Assignment"] = task.Title, ["DueDate"] = due,
                            ["SubmissionDate"] = submittedOn, ["DelayDays"] = Math.Max(0, LearningExportDelay(fact)).ToString(), ["Subject"] = task.SubjectName };
                        break;
                    case AcademicReportTypes.MissingAssignmentReport:
                        values = new() { ["Student"] = fact.StudentName, ["Assignment"] = task.Title, ["Subject"] = task.SubjectName,
                            ["DueDate"] = due, ["PendingDays"] = Math.Max(0, (today - fact.DueDate.Date).Days).ToString(), ["Teacher"] = task.TeacherName };
                        break;
                    case AcademicReportTypes.AssignmentFeedbackReport:
                        if (string.IsNullOrWhiteSpace(fact.Feedback) && string.IsNullOrWhiteSpace(fact.Remarks)) continue;
                        values = new() { ["Student"] = fact.StudentName, ["Subject"] = task.SubjectName, ["Assignment"] = task.Title,
                            ["Teacher"] = fact.ReviewerName, ["FeedbackDate"] = fact.ReviewedAt?.ToString("yyyy-MM-dd") ?? "—",
                            ["FeedbackRemarks"] = !string.IsNullOrWhiteSpace(fact.Feedback) ? fact.Feedback : fact.Remarks, ["Rating"] = "—" };
                        break;
                    default: throw new InvalidOperationException($"Unsupported learning task export: {kind}");
                }
                yield return new(fact, values);
            }
        }
    }

    private async Task EnrichLearningExportAsync(List<LearningExportSubmission> batch, CancellationToken cancellationToken)
    {
        var subjectIds = batch.Select(s => s.Task.SubjectId).Distinct().ToList();
        var standardIds = batch.Select(s => s.Task.StandardId).Distinct().ToList();
        var divisionIds = batch.Select(s => s.DivisionId).Distinct().ToList();
        var studentIds = batch.Select(s => s.StudentId).Where(id => id != Guid.Empty).Distinct().ToList();
        var userIds = batch.SelectMany(s => new[] { s.Task.CreatorId, s.ReviewedBy }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var subjects = await subjectRepository.GetQueryableAsync();
        var subjectNames = (await asyncExecuter.ToListAsync(subjects.Where(s => subjectIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }), cancellationToken)).ToDictionary(s => s.Id, s => s.Name);
        var standards = await standardRepository.GetQueryableAsync();
        var standardNames = (await asyncExecuter.ToListAsync(standards.Where(s => standardIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }), cancellationToken)).ToDictionary(s => s.Id, s => s.Name);
        var divisions = await divisionRepository.GetQueryableAsync();
        var divisionNames = (await asyncExecuter.ToListAsync(divisions.Where(s => divisionIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }), cancellationToken)).ToDictionary(s => s.Id, s => s.Name);
        var teacherNames = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            var teachers = await employeeRepository.GetQueryableAsync();
            teacherNames = (await asyncExecuter.ToListAsync(teachers.Where(e => e.UserId.HasValue && userIds.Contains(e.UserId.Value))
                .Select(e => new { e.UserId, e.FirstName, e.LastName }), cancellationToken)).ToDictionary(e => e.UserId!.Value,
                e => string.Join(" ", new[] { e.FirstName, e.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))));
        }
        var studentNames = new Dictionary<Guid, string>();
        if (studentIds.Count > 0)
        {
            var students = await studentRepository.GetQueryableAsync();
            studentNames = (await asyncExecuter.ToListAsync(students.Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName }), cancellationToken)).ToDictionary(s => s.Id,
                s => string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))).Trim());
        }
        foreach (var fact in batch)
        {
            fact.Task.SubjectName = subjectNames.GetValueOrDefault(fact.Task.SubjectId, "—");
            fact.Task.SubjectSortName = subjectNames.GetValueOrDefault(fact.Task.SubjectId, string.Empty);
            fact.Task.StandardName = standardNames.GetValueOrDefault(fact.Task.StandardId, "—");
            fact.Task.StandardSortName = standardNames.GetValueOrDefault(fact.Task.StandardId, string.Empty);
            fact.Task.TeacherName = fact.Task.CreatorId.HasValue ? teacherNames.GetValueOrDefault(fact.Task.CreatorId.Value, "—") : "—";
            fact.Task.TeacherSortName = fact.Task.CreatorId.HasValue ? teacherNames.GetValueOrDefault(fact.Task.CreatorId.Value, string.Empty) : string.Empty;
            fact.DivisionName = divisionNames.GetValueOrDefault(fact.DivisionId, "—");
            fact.DivisionSortName = divisionNames.GetValueOrDefault(fact.DivisionId, string.Empty);
            fact.StudentName = studentNames.GetValueOrDefault(fact.StudentId);
            fact.ReviewerName = fact.ReviewedBy.HasValue ? teacherNames.GetValueOrDefault(fact.ReviewedBy.Value, "—") : "—";
            fact.ReviewerSortName = fact.ReviewedBy.HasValue ? teacherNames.GetValueOrDefault(fact.ReviewedBy.Value, string.Empty) : string.Empty;
        }
    }

    private static int LearningExportDelay(LearningExportSubmission fact) => fact.SubmissionDate.HasValue
        ? (fact.SubmissionDate.Value.UtcDateTime.Date - fact.DueDate.Date).Days : 0;

    private static int CompareLearningExportRows(LearningExportRow left, LearningExportRow right, string kind)
    {
        var a = left.Source;
        var b = right.Source;
        var strings = Comparer<string?>.Default;
        return kind switch
        {
            AcademicReportTypes.PendingHomeworkReport or AcademicReportTypes.MissingAssignmentReport => a.DueDate.CompareTo(b.DueDate),
            AcademicReportTypes.OverdueHomeworkReport => a.DueDate.Date.CompareTo(b.DueDate.Date),
            AcademicReportTypes.HomeworkSubmissionHistoryReport => (b.SubmissionDate ?? DateTimeOffset.MinValue).CompareTo(a.SubmissionDate ?? DateTimeOffset.MinValue),
            AcademicReportTypes.HomeworkAcknowledgementReport or AcademicReportTypes.AssignmentSubmissionReport => a.Task.Id != b.Task.Id
                ? a.Task.Id.CompareTo(b.Task.Id) : strings.Compare(a.StudentName, b.StudentName),
            AcademicReportTypes.AssignmentMarksReport => strings.Compare(a.StudentName, b.StudentName) is var comparison && comparison != 0
                ? comparison : a.Task.Id.CompareTo(b.Task.Id),
            AcademicReportTypes.LateSubmissionReport => LearningExportDelay(b).CompareTo(LearningExportDelay(a)),
            AcademicReportTypes.AssignmentFeedbackReport => (a.ReviewedAt ?? DateTimeOffset.MaxValue).CompareTo(b.ReviewedAt ?? DateTimeOffset.MaxValue),
            AcademicReportTypes.HomeworkWithAttachmentsReport or AcademicReportTypes.DailyHomeworkReport or AcademicReportTypes.AssignmentIssuanceReport
                or AcademicReportTypes.HomeworkCompletionStatusReport => a.Task.CreationTime.CompareTo(b.Task.CreationTime),
            AcademicReportTypes.ClasswiseHomeworkReport => a.Task.StandardId != b.Task.StandardId ? a.Task.StandardId.CompareTo(b.Task.StandardId) : a.Task.SubjectId.CompareTo(b.Task.SubjectId),
            AcademicReportTypes.SubjectwiseHomeworkReport => strings.Compare(a.Task.SubjectSortName, b.Task.SubjectSortName) is var subjectOrder && subjectOrder != 0
                ? subjectOrder : a.Task.Id.CompareTo(b.Task.Id),
            AcademicReportTypes.TeacherwiseHomeworkReport => strings.Compare(a.Task.TeacherSortName, b.Task.TeacherSortName) is var teacherOrder && teacherOrder != 0
                ? teacherOrder : a.Task.Id.CompareTo(b.Task.Id) is var taskOrder && taskOrder != 0 ? taskOrder : a.Id.CompareTo(b.Id),
            AcademicReportTypes.StudentwiseHomeworkSummaryReport => strings.Compare(a.StudentName, b.StudentName) is var studentOrder && studentOrder != 0
                ? studentOrder : a.SourceOrdinal.CompareTo(b.SourceOrdinal),
            AcademicReportTypes.AssignmentEvaluationReport => strings.Compare(a.Task.Title, b.Task.Title),
            AcademicReportTypes.AssignmentPerformanceReport => strings.Compare(a.Task.StandardSortName, b.Task.StandardSortName) is var standardOrder && standardOrder != 0
                ? standardOrder : strings.Compare(a.Task.SubjectSortName, b.Task.SubjectSortName) is var performanceSubjectOrder && performanceSubjectOrder != 0
                    ? performanceSubjectOrder : a.Task.Id.CompareTo(b.Task.Id),
            AcademicReportTypes.AssignmentGradeDistributionReport => a.SourceOrdinal.CompareTo(b.SourceOrdinal),
            _ => 0
        };
    }

    private sealed class LearningExportTask
    {
        public Guid Id { get; set; }
        public Guid StandardId { get; set; }
        public Guid SubjectId { get; set; }
        public Guid? CreatorId { get; set; }
        public DateTime CreationTime { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal TotalMarks { get; set; }
        public int Status { get; set; }
        public string? AttachmentsJson { get; set; }
        public string? DocumentUrl { get; set; }
        public string SubjectName { get; set; } = "—";
        public string StandardName { get; set; } = "—";
        public string TeacherName { get; set; } = "—";
        public string SubjectSortName { get; set; } = string.Empty;
        public string StandardSortName { get; set; } = string.Empty;
        public string TeacherSortName { get; set; } = string.Empty;
    }

    private sealed class LearningExportSubmission
    {
        public long SourceOrdinal { get; set; }
        public LearningExportTask Task { get; set; } = new();
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public Guid DivisionId { get; set; }
        public DateTime DueDate { get; set; }
        public DateTimeOffset? SubmissionDate { get; set; }
        public SubmissionStatus Status { get; set; }
        public decimal? Marks { get; set; }
        public decimal? TotalMarks { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public string? Feedback { get; set; }
        public string? Remarks { get; set; }
        public string? StudentName { get; set; }
        public string DivisionName { get; set; } = "—";
        public string ReviewerName { get; set; } = "—";
        public string ReviewerSortName { get; set; } = string.Empty;
        public string DivisionSortName { get; set; } = string.Empty;
    }

    private sealed record LearningExportRow(LearningExportSubmission Source, Dictionary<string, string?> Values);
}
