using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Examination.Results;

[RemoteService(false)]
public class ExamResultAppService(
    IExamRepository examRepository,
    IRepository<ExamResultHeader, Guid> resultHeaderRepository,
    IRepository<ExamResultDetail, Guid> resultDetailRepository,
    IRepository<ExamResultAuditLog, Guid> resultAuditLogRepository,
    IRepository<ExamMark, Guid> examMarkRepository,
    IRepository<ExamMarksWorkflowState, Guid> workflowStateRepository,
    IRepository<GradingScale, Guid> gradingScaleRepository) : ApplicationService, IExamResultAppService
{
    // ─────────────────────────────────────────────────────────────────────────
    // Readiness check
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultReadinessDto> GetReadinessAsync(Guid examId, Guid divisionId)
    {
        var checks = new List<ReadinessCheckDto>();

        var exam = await examRepository.GetAsync(examId, includeDetails: true);
        var subjectIds = exam.ExamSubjects.Select(s => s.SubjectId).ToList();

        // Check 1: At least one subject configured
        checks.Add(new ReadinessCheckDto
        {
            Label = "Exam subjects configured",
            Passed = exam.ExamSubjects.Any(),
            Hint = !exam.ExamSubjects.Any() ? "No subjects are configured for this exam." : null
        });

        // Check 2: All marks verified (Verified or Published workflow state)
        var workflowStates = await workflowStateRepository.GetListAsync(
            s => s.ExamId == examId && s.DivisionId == divisionId);
        var verifiedCount = workflowStates.Count(
            s => s.Status == ExamMarksWorkflowStatus.Verified || s.Status == ExamMarksWorkflowStatus.Published);
        var totalSubjects = exam.ExamSubjects.Count;
        checks.Add(new ReadinessCheckDto
        {
            Label = "Marks verified for all subjects",
            Passed = totalSubjects > 0 && verifiedCount >= totalSubjects,
            Hint = verifiedCount < totalSubjects
                ? $"{totalSubjects - verifiedCount} subject(s) not yet verified."
                : null
        });

        // Check 3: Marks entries exist
        var marksCount = await examMarkRepository.CountAsync(
            m => m.ExamId == examId && m.DivisionId == divisionId);
        checks.Add(new ReadinessCheckDto
        {
            Label = "Marks data available",
            Passed = marksCount > 0,
            Hint = marksCount == 0 ? "No marks found for this division." : null
        });

        // Check 4: Grading scale assigned to at least one subject
        var hasScale = exam.ExamSubjects.Any(s => s.GradeScalingId.HasValue);
        checks.Add(new ReadinessCheckDto
        {
            Label = "Grading scale assigned",
            Passed = hasScale,
            Hint = !hasScale ? "No grading scale assigned to exam subjects; fallback grading will be used." : null
        });

        return new ResultReadinessDto
        {
            ExamId = examId,
            DivisionId = divisionId,
            IsReady = checks.Take(3).All(c => c.Passed),   // scale is a soft warning, not a hard blocker
            Checks = checks
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generate / Regenerate
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Generate)]
    public async Task<ResultHeaderDto> GenerateAsync(GenerateResultRequestDto input)
    {
        var exam = await examRepository.GetAsync(input.ExamId, includeDetails: true);

        // Guard: Frozen without ForceRegenerate
        var existing = await resultHeaderRepository.FirstOrDefaultAsync(
            h => h.ExamId == input.ExamId && h.DivisionId == input.DivisionId);
        if (existing?.Status == ExamResultStatus.Frozen && !input.ForceRegenerate)
            throw new UserFriendlyException("Result is frozen. Set ForceRegenerate=true to override.");

        // Load grading scale from the first subject that has one
        var scaleId = exam.ExamSubjects.FirstOrDefault(s => s.GradeScalingId.HasValue)?.GradeScalingId;
        ICollection<GradeRange>? gradeRanges = null;
        if (scaleId.HasValue)
        {
            var scaleQuery = await gradingScaleRepository.WithDetailsAsync(s => s.GradeRanges);
            var scale = scaleQuery.FirstOrDefault(s => s.Id == scaleId.Value);
            gradeRanges = scale?.GradeRanges;
        }

        var totalMaxMarks = exam.ExamSubjects.Sum(s => s.TotalMarks ?? 0);
        var totalPassMarks = exam.ExamSubjects.Sum(s => s.PassingMarks ?? 0);
        var subjectPassMap = exam.ExamSubjects
            .Where(s => s.PassingMarks.HasValue)
            .ToDictionary(s => s.SubjectId, s => s.PassingMarks!.Value);

        // Load marks
        var marks = await examMarkRepository.GetListAsync(
            m => m.ExamId == input.ExamId && m.DivisionId == input.DivisionId);

        // Compute per-student results
        var userId = CurrentUser.GetId();
        var tenantId = CurrentTenant.Id;

        var studentResults = marks
            .GroupBy(m => m.StudentId)
            .Select(g =>
            {
                var isAbsent = g.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0.0 : g.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = totalMaxMarks > 0 ? Math.Round(obtained / totalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, totalMaxMarks, gradeRanges);

                // Compartment: passed overall but failed at least one subject
                var failedAnySubject = !isAbsent && subjectPassMap.Count > 0 &&
                    g.Any(m => !m.IsAbsent &&
                               subjectPassMap.TryGetValue(m.SubjectId, out var sp) &&
                               (m.MarksObtained ?? 0) < sp);

                var status = isAbsent ? ExamStudentResultStatus.Absent
                    : obtained >= totalPassMarks && !failedAnySubject ? ExamStudentResultStatus.Pass
                    : obtained >= totalPassMarks && failedAnySubject ? ExamStudentResultStatus.Compartment
                    : ExamStudentResultStatus.Fail;

                return new
                {
                    StudentId = g.Key,
                    TotalObtainedMarks = obtained,
                    Percentage = pct,
                    Grade = grade,
                    Status = status
                };
            })
            .OrderByDescending(x => x.TotalObtainedMarks)
            .ToList();

        // Assign class rank (ties share rank, next rank skips)
        var rankedResults = new List<(Guid StudentId, double Obtained, double Pct, string Grade,
            ExamStudentResultStatus Status, int? Rank)>();
        int rank = 1, tieStart = 0;
        double lastMarks = -1;
        for (int i = 0; i < studentResults.Count; i++)
        {
            var r = studentResults[i];
            if (r.Status == ExamStudentResultStatus.Absent)
            {
                rankedResults.Add((r.StudentId, r.TotalObtainedMarks, r.Percentage, r.Grade, r.Status, null));
                continue;
            }
            if (r.TotalObtainedMarks != lastMarks)
            {
                rank = i + 1;
                lastMarks = r.TotalObtainedMarks;
            }
            rankedResults.Add((r.StudentId, r.TotalObtainedMarks, r.Percentage, r.Grade, r.Status, rank));
        }

        // Upsert ExamResultHeader
        ExamResultHeader header;
        string auditAction;
        if (existing != null)
        {
            // Delete previous detail rows
            await resultDetailRepository.DeleteAsync(d => d.ResultHeaderId == existing.Id, autoSave: false);
            existing.Regenerate(userId);
            header = await resultHeaderRepository.UpdateAsync(existing, autoSave: false);
            auditAction = "Regenerated";
        }
        else
        {
            header = new ExamResultHeader(GuidGenerator.Create(), input.ExamId, input.DivisionId, userId, tenantId);
            header = await resultHeaderRepository.InsertAsync(header, autoSave: false);
            auditAction = "Generated";
        }

        // Insert detail rows
        var details = rankedResults
            .Select(r => new ExamResultDetail(
                GuidGenerator.Create(),
                header.Id,
                input.ExamId,
                r.StudentId,
                input.DivisionId,
                totalMaxMarks,
                r.Obtained,
                r.Pct,
                r.Grade,
                r.Rank,
                null,   // sectionRank
                r.Status,
                tenantId))
            .ToList();

        await resultDetailRepository.InsertManyAsync(details, autoSave: false);

        // Audit log
        var log = new ExamResultAuditLog(
            GuidGenerator.Create(), input.ExamId, input.DivisionId,
            auditAction, userId, null, tenantId);
        await resultAuditLogRepository.InsertAsync(log, autoSave: true);

        return MapToHeaderDto(header, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Publish
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Publish)]
    public async Task<ResultHeaderDto> PublishAsync(PublishResultRequestDto input)
    {
        var header = await resultHeaderRepository.GetAsync(input.ResultHeaderId);
        var userId = CurrentUser.GetId();

        header.Publish(userId);
        await resultHeaderRepository.UpdateAsync(header, autoSave: false);

        var log = new ExamResultAuditLog(
            GuidGenerator.Create(), header.ExamId, header.DivisionId,
            "Published", userId, null, CurrentTenant.Id);
        await resultAuditLogRepository.InsertAsync(log, autoSave: true);

        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == header.Id);
        return MapToHeaderDto(header, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Freeze
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Freeze)]
    public async Task<ResultHeaderDto> FreezeAsync(FreezeResultRequestDto input)
    {
        var header = await resultHeaderRepository.GetAsync(input.ResultHeaderId);
        var userId = CurrentUser.GetId();

        header.Freeze(userId, input.Remarks);
        await resultHeaderRepository.UpdateAsync(header, autoSave: false);

        var log = new ExamResultAuditLog(
            GuidGenerator.Create(), header.ExamId, header.DivisionId,
            "Frozen", userId, input.Remarks, CurrentTenant.Id);
        await resultAuditLogRepository.InsertAsync(log, autoSave: true);

        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == header.Id);
        return MapToHeaderDto(header, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultAnalyticsDto> GetAnalyticsAsync(Guid resultHeaderId)
    {
        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == resultHeaderId);
        return ComputeAnalytics(resultHeaderId, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Get Header
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultHeaderDto?> GetHeaderAsync(Guid examId, Guid divisionId)
    {
        var header = await resultHeaderRepository.FirstOrDefaultAsync(
            h => h.ExamId == examId && h.DivisionId == divisionId);
        if (header is null) return null;

        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == header.Id);
        return MapToHeaderDto(header, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ComputeGrade(double obtained, double total, ICollection<GradeRange>? ranges = null)
    {
        if (total <= 0) return "—";
        if (ranges?.Any() == true)
        {
            var pct = obtained / total * 100;
            var match = ranges
                .OrderByDescending(r => r.MinMarks)
                .FirstOrDefault(r => pct >= r.MinMarks && pct <= r.MaxMarks);
            if (match != null) return match.Grade;
        }
        var p = obtained / total * 100;
        return p >= 90 ? "A+" : p >= 80 ? "A" : p >= 70 ? "B+" : p >= 60 ? "B"
             : p >= 50 ? "C+" : p >= 40 ? "C" : p >= 33 ? "D" : "F";
    }

    private static ResultHeaderDto MapToHeaderDto(
        ExamResultHeader header,
        IList<ExamResultDetail> details)
    {
        var appeared = details.Count(d => d.ResultStatus != ExamStudentResultStatus.Absent);
        var passed = details.Count(d => d.ResultStatus == ExamStudentResultStatus.Pass);
        var failed = details.Count(d => d.ResultStatus == ExamStudentResultStatus.Fail);
        var compartment = details.Count(d => d.ResultStatus == ExamStudentResultStatus.Compartment);
        var absent = details.Count(d => d.ResultStatus == ExamStudentResultStatus.Absent);
        var passPercent = appeared > 0 ? Math.Round((double) (passed + compartment) / appeared * 100, 1) : 0;
        var highest = details.Any(d => d.ResultStatus != ExamStudentResultStatus.Absent)
            ? details.Where(d => d.ResultStatus != ExamStudentResultStatus.Absent).Max(d => d.Percentage)
            : 0;
        var average = appeared > 0
            ? Math.Round(details.Where(d => d.ResultStatus != ExamStudentResultStatus.Absent)
                .Average(d => d.Percentage), 1)
            : 0;

        return new ResultHeaderDto
        {
            Id = header.Id,
            ExamId = header.ExamId,
            DivisionId = header.DivisionId,
            Status = header.Status.ToString(),
            StatusCode = (int) header.Status,
            GeneratedOn = header.GeneratedOn,
            PublishedOn = header.PublishedOn,
            FrozenOn = header.FrozenOn,
            Remarks = header.Remarks,
            TotalStudents = details.Count,
            Appeared = appeared,
            Passed = passed,
            Failed = failed,
            Compartment = compartment,
            Absent = absent,
            PassPercentage = passPercent,
            HighestPercentage = highest,
            AveragePercentage = average
        };
    }

    private static ResultAnalyticsDto ComputeAnalytics(Guid headerId, IList<ExamResultDetail> details)
    {
        var appeared = details.Where(d => d.ResultStatus != ExamStudentResultStatus.Absent).ToList();
        var highest = appeared.Any() ? appeared.Max(d => d.Percentage) : 0;
        var lowest = appeared.Any() ? appeared.Min(d => d.Percentage) : 0;
        var average = appeared.Any() ? Math.Round(appeared.Average(d => d.Percentage), 1) : 0;
        var passed = appeared.Count(d => d.ResultStatus == ExamStudentResultStatus.Pass);
        var compartment = appeared.Count(d => d.ResultStatus == ExamStudentResultStatus.Compartment);
        var passPercent = appeared.Count > 0
            ? Math.Round((double) (passed + compartment) / appeared.Count * 100, 1)
            : 0;

        return new ResultAnalyticsDto
        {
            ResultHeaderId = headerId,
            TotalStudents = details.Count,
            Appeared = appeared.Count,
            Passed = passed,
            Failed = details.Count(d => d.ResultStatus == ExamStudentResultStatus.Fail),
            Compartment = compartment,
            Absent = details.Count(d => d.ResultStatus == ExamStudentResultStatus.Absent),
            PassPercentage = passPercent,
            HighestPercentage = highest,
            LowestPercentage = lowest,
            AveragePercentage = average,
            GradeDistribution = details
                .GroupBy(d => d.Grade)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}
