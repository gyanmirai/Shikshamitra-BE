using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Security.UserScope;
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
    IRepository<GradingScale, Guid> gradingScaleRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<Subject, Guid> subjectRepository,
    StudentExamSubjectApplicabilityResolver subjectApplicabilityResolver,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IUserScopeService userScopeService,
    ExamPrimaryTeacherAccessService examPrimaryTeacherAccessService) : ApplicationService, IExamResultAppService
{
    // ─────────────────────────────────────────────────────────────────────────
    // Readiness check
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultReadinessDto> GetReadinessAsync(Guid examId, Guid divisionId)
    {
        var exam = await GetScopedExamAsync(examId, divisionId);
        var inputs = await LoadResultInputsAsync(exam, divisionId);
        return await GetReadinessAsync(exam, divisionId, inputs);
    }

    private async Task<ResultInputs> LoadResultInputsAsync(Exam exam, Guid divisionId)
    {
        var enrolledStudents = await studentRepository.GetListAsync(s =>
            s.DivisionId == divisionId &&
            s.Status == StudentLifeCycleStatusEnum.Enrolled &&
            !s.IsDeleted);
        var applicableSubjectIdsByStudent = await subjectApplicabilityResolver.ResolveAsync(exam, enrolledStudents);
        var marks = await examMarkRepository.GetListAsync(
            mark => mark.ExamId == exam.Id && mark.DivisionId == divisionId);
        return new ResultInputs(enrolledStudents, applicableSubjectIdsByStudent, marks);
    }

    private async Task<ResultReadinessDto> GetReadinessAsync(Exam exam, Guid divisionId, ResultInputs inputs)
    {
        var examId = exam.Id;
        var checks = new List<ReadinessCheckDto>();
        var applicableSubjectIdsByStudent = inputs.ApplicableSubjectIdsByStudent;
        var requiredSubjectIds = applicableSubjectIdsByStudent.Values
            .SelectMany(subjectIds => subjectIds)
            .ToHashSet();

        // Check 1: At least one subject configured
        checks.Add(new ReadinessCheckDto
        {
            Label = "Exam subjects configured",
            Passed = exam.ExamSubjects.Any(),
            Hint = !exam.ExamSubjects.Any() ? "No subjects are configured for this exam." : null,
            Severity = exam.ExamSubjects.Any() ? "success" : "blocker",
            FixTarget = exam.ExamSubjects.Any() ? null : "exam-setup"
        });

        // Check 2: All marks published/locked
        var workflowStates = await workflowStateRepository.GetListAsync(
            s => s.ExamId == examId && s.DivisionId == divisionId);
        var verifiedSubjectIds = workflowStates
            .Where(s => s.Status == ExamMarksWorkflowStatus.Published && requiredSubjectIds.Contains(s.SubjectId))
            .Select(s => s.SubjectId)
            .ToHashSet();
        var verifiedCount = verifiedSubjectIds.Count;
        var totalSubjects = requiredSubjectIds.Count;
        var pendingSubjectIds = exam.ExamSubjects
            .Where(s => requiredSubjectIds.Contains(s.SubjectId) && !verifiedSubjectIds.Contains(s.SubjectId))
            .Select(s => s.SubjectId)
            .ToList();
        var pendingSubjects = pendingSubjectIds.Count == 0
            ? new List<string>()
            : (await subjectRepository.GetListAsync(s => pendingSubjectIds.Contains(s.Id)))
                .Select(s => s.Name)
                .OrderBy(name => name)
                .ToList();
        var completedSubjects = Math.Min(verifiedCount, totalSubjects);
        var allMarksVerified = totalSubjects > 0 && verifiedCount >= totalSubjects;
        checks.Add(new ReadinessCheckDto
        {
            Label = "Marks published and locked for all subjects",
            Passed = allMarksVerified,
            Hint = verifiedCount < totalSubjects
                ? $"{totalSubjects - verifiedCount} subject(s) not yet published."
                : null,
            Severity = allMarksVerified ? "success" : "blocker",
            FixTarget = allMarksVerified ? null : "grade-entry"
        });

        // Check 3: Marks entries exist
        var marks = inputs.Marks;
        var expectedMarkRows = applicableSubjectIdsByStudent.Values.Sum(subjectIds => subjectIds.Count);
        var markKeys = marks
            .Where(mark => applicableSubjectIdsByStudent.TryGetValue(mark.StudentId, out var subjectIds) &&
                subjectIds.Contains(mark.SubjectId))
            .Select(mark => (mark.StudentId, mark.SubjectId))
            .ToHashSet();
        var availableMarkRows = applicableSubjectIdsByStudent.Sum(entry =>
            entry.Value.Count(subjectId => markKeys.Contains((entry.Key, subjectId))));
        var missingMarks = Math.Max(expectedMarkRows - availableMarkRows, 0);
        var hasMarks = availableMarkRows > 0;
        var hasAllMarkRows = expectedMarkRows > 0 && missingMarks == 0;
        checks.Add(new ReadinessCheckDto
        {
            Label = "Marks data available",
            Passed = hasMarks && hasAllMarkRows,
            Hint = availableMarkRows == 0
                ? "No marks found for this division."
                : !hasAllMarkRows
                    ? $"{missingMarks} student-subject mark row(s) are missing."
                    : null,
            Severity = hasMarks && hasAllMarkRows ? "success" : "blocker",
            FixTarget = hasMarks && hasAllMarkRows ? null : "grade-entry"
        });

        // Check 4: Grading scale assigned to at least one subject
        var hasScale = exam.ExamSubjects.Any(s => s.GradeScalingId.HasValue);
        checks.Add(new ReadinessCheckDto
        {
            Label = "Grading scale assigned",
            Passed = hasScale,
            Hint = !hasScale ? "No grading scale assigned to exam subjects; fallback grading will be used." : null,
            Severity = hasScale ? "success" : "warning",
            FixTarget = hasScale ? null : "grade-scaling"
        });

        var requiredChecks = checks.Take(3).ToList();
        var blockerCount = checks.Count(c => !c.Passed && c.Severity == "blocker");
        var warningCount = checks.Count(c => !c.Passed && c.Severity == "warning");
        var percentage = requiredChecks.Count == 0
            ? 0
            : (int)Math.Round(requiredChecks.Count(c => c.Passed) / (double)requiredChecks.Count * 100);

        return new ResultReadinessDto
        {
            ExamId = examId,
            DivisionId = divisionId,
            IsReady = blockerCount == 0,   // scale is a soft warning, not a hard blocker
            Percentage = percentage,
            RequiredSubjects = totalSubjects,
            CompletedSubjects = completedSubjects,
            MissingMarks = missingMarks,
            BlockerCount = blockerCount,
            WarningCount = warningCount,
            PendingSubjects = pendingSubjects,
            Checks = checks
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generate / Regenerate
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Generate)]
    public async Task<ResultHeaderDto> GenerateAsync(GenerateResultRequestDto input)
    {
        var exam = await GetScopedExamAsync(input.ExamId, input.DivisionId);
        if (exam.ExamDivisions.All(x => x.DivisionId != input.DivisionId))
            throw new UserFriendlyException("Selected division does not belong to this exam.");

        var existing = await resultHeaderRepository.FirstOrDefaultAsync(
            h => h.ExamId == input.ExamId && h.DivisionId == input.DivisionId);
        if (existing != null)
        {
            if (existing.Status is ExamResultStatus.Published or ExamResultStatus.Frozen)
            {
                throw new UserFriendlyException("Published or frozen results cannot be regenerated directly. Use the unlock/correction workflow first.");
            }

            if (!input.ForceRegenerate)
            {
                throw new UserFriendlyException("Result is already generated. Set ForceRegenerate=true to regenerate the draft result.");
            }
        }

        var resultInputs = await LoadResultInputsAsync(exam, input.DivisionId);
        var readiness = await GetReadinessAsync(exam, input.DivisionId, resultInputs);
        if (!readiness.IsReady)
            throw new UserFriendlyException("Result cannot be generated until all blocking readiness checks pass.");

        // Load grading scale from the overall exam
        var scaleId = exam.GradingScaleId;
        ICollection<GradeRange>? gradeRanges = null;
        double scaleTotal = 100.0;
        if (scaleId.HasValue)
        {
            var scaleQuery = await gradingScaleRepository.WithDetailsAsync(s => s.GradeRanges);
            var scale = await AsyncExecuter.FirstOrDefaultAsync(scaleQuery.Where(s => s.Id == scaleId.Value));
            gradeRanges = scale?.GradeRanges;
            if (scale != null && double.TryParse(scale.TotalMarks, out var parsedTotal))
            {
                scaleTotal = parsedTotal;
            }
        }

        // Load marks
        var marks = resultInputs.Marks;
        if (marks.Count == 0)
            throw new UserFriendlyException("No marks found for this exam and division.");

        var enrolledStudents = resultInputs.EnrolledStudents;
        if (enrolledStudents.Count == 0)
            throw new UserFriendlyException("No enrolled students found for this division.");

        var enrolledStudentIds = enrolledStudents.Select(s => s.Id).ToHashSet();
        marks = marks.Where(m => enrolledStudentIds.Contains(m.StudentId)).ToList();
        if (marks.Count == 0)
            throw new UserFriendlyException("No valid marks found for currently enrolled students in this division.");

        // Compute per-student results
        var userId = CurrentUser.GetId();
        var tenantId = CurrentTenant.Id;

        var marksByStudent = marks
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var applicableSubjectIdsByStudent = resultInputs.ApplicableSubjectIdsByStudent;

        var studentResults = enrolledStudents
            .Select(g =>
            {
                marksByStudent.TryGetValue(g.Id, out var studentMarks);
                studentMarks ??= new List<ExamMark>();
                var applicableSubjectIds = applicableSubjectIdsByStudent[g.Id];
                var applicableExamSubjects = exam.ExamSubjects
                    .Where(subject => applicableSubjectIds.Contains(subject.SubjectId))
                    .ToList();
                studentMarks = studentMarks
                    .Where(mark => applicableSubjectIds.Contains(mark.SubjectId))
                    .ToList();

                var totalMaxMarks = applicableExamSubjects.Sum(s => s.TotalMarks ?? 0);
                var totalPassMarks = applicableExamSubjects.Sum(s =>
                    s.PassingMarks ?? ((s.TotalMarks ?? 100.0) * 0.33));
                var isAbsent = applicableExamSubjects.Count == 0 ||
                    studentMarks.Count == 0 ||
                    studentMarks.All(m => m.IsAbsent);
                var obtained = isAbsent ? 0.0 : studentMarks.Where(m => !m.IsAbsent).Sum(m => m.MarksObtained ?? 0);
                var pct = totalMaxMarks > 0 ? Math.Round(obtained / totalMaxMarks * 100, 2) : 0;
                var grade = isAbsent ? "—" : ComputeGrade(obtained, totalMaxMarks, gradeRanges, scaleTotal);

                // Check if they failed any subject (missing mark, absent, or obtained < passing marks)
                var failedAnySubject = !isAbsent && applicableExamSubjects.Any(s =>
                {
                    var mark = studentMarks.FirstOrDefault(m => m.SubjectId == s.SubjectId);
                    if (mark == null || mark.IsAbsent) return true;
                    var passingMarksVal = s.PassingMarks ?? ((s.TotalMarks ?? 100.0) * 0.33);
                    return (mark.MarksObtained ?? 0) < passingMarksVal;
                });

                var status = isAbsent ? ExamStudentResultStatus.Absent
                    : obtained >= totalPassMarks && !failedAnySubject ? ExamStudentResultStatus.Pass
                    : ExamStudentResultStatus.Fail;

                return new
                {
                    StudentId = g.Id,
                    TotalMaxMarks = totalMaxMarks,
                    TotalObtainedMarks = obtained,
                    Percentage = pct,
                    Grade = grade,
                    Status = status
                };
            })
            .OrderByDescending(x => x.Percentage)
            .ThenByDescending(x => x.TotalObtainedMarks)
            .ToList();

        // Assign class rank (ties share rank, next rank skips)
        var rankedResults = new List<(Guid StudentId, double TotalMaxMarks, double Obtained, double Pct, string Grade,
            ExamStudentResultStatus Status, int? Rank)>();
        int rank = 1;
        double lastPercentage = -1;
        for (int i = 0; i < studentResults.Count; i++)
        {
            var r = studentResults[i];
            if (r.Status == ExamStudentResultStatus.Absent)
            {
                rankedResults.Add((r.StudentId, r.TotalMaxMarks, r.TotalObtainedMarks, r.Percentage, r.Grade, r.Status, null));
                continue;
            }
            if (r.Percentage != lastPercentage)
            {
                rank = i + 1;
                lastPercentage = r.Percentage;
            }
            rankedResults.Add((r.StudentId, r.TotalMaxMarks, r.TotalObtainedMarks, r.Percentage, r.Grade, r.Status, rank));
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
                r.TotalMaxMarks,
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
        await GetScopedExamAsync(header.ExamId, header.DivisionId);
        var userId = CurrentUser.GetId();
        if (header.Status != ExamResultStatus.Generated)
            throw new UserFriendlyException("Only generated results can be published.");

        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == header.Id);
        if (details.Count == 0)
            throw new UserFriendlyException("Result has no student details to publish.");

        header.Publish(userId);
        await resultHeaderRepository.UpdateAsync(header, autoSave: false);

        var log = new ExamResultAuditLog(
            GuidGenerator.Create(), header.ExamId, header.DivisionId,
            "Published", userId, null, CurrentTenant.Id);
        await resultAuditLogRepository.InsertAsync(log, autoSave: true);

        return MapToHeaderDto(header, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Freeze
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.Freeze)]
    public async Task<ResultHeaderDto> FreezeAsync(FreezeResultRequestDto input)
    {
        var header = await resultHeaderRepository.GetAsync(input.ResultHeaderId);
        await GetScopedExamAsync(header.ExamId, header.DivisionId);
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
        var header = await resultHeaderRepository.GetAsync(resultHeaderId);
        await GetScopedExamAsync(header.ExamId, header.DivisionId);
        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == resultHeaderId);
        return ComputeAnalytics(resultHeaderId, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Get Header
    // ─────────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<ResultHeaderDto?> GetHeaderAsync(Guid examId, Guid divisionId)
    {
        await GetScopedExamAsync(examId, divisionId);
        var header = await resultHeaderRepository.FirstOrDefaultAsync(
            h => h.ExamId == examId && h.DivisionId == divisionId);
        if (header is null) return null;

        var details = await resultDetailRepository.GetListAsync(d => d.ResultHeaderId == header.Id);
        return MapToHeaderDto(header, details);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Exam> GetScopedExamAsync(Guid examId, Guid? divisionId = null)
    {
        var exam = await examRepository.GetAsync(examId, includeDetails: true);
        if (divisionId.HasValue && exam.ExamDivisions.All(row => row.DivisionId != divisionId.Value))
        {
            throw new UserFriendlyException("Selected division does not belong to this exam.");
        }

        if (!CurrentUser.Id.HasValue)
        {
            throw new BusinessException(UserScopeErrorCodes.NoCurrentUser);
        }

        var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
        if (scope.IsAdmin || scope.HasTenantWideScope() ||
            scope.StandardIds.Contains(exam.StandardId) ||
            scope.WildcardDivisionStandardIds.Contains(exam.StandardId) ||
            await examPrimaryTeacherAccessService.HasAccessAsync(CurrentUser.Id.Value, exam))
        {
            return exam;
        }

        var targetDivisionIds = divisionId.HasValue
            ? new[] { divisionId.Value }
            : exam.ExamDivisions.Select(row => row.DivisionId).Distinct().ToArray();
        if (targetDivisionIds.Length > 0 && targetDivisionIds.All(scope.DivisionIds.Contains))
        {
            return exam;
        }

        throw new BusinessException(UserScopeErrorCodes.AccessDenied)
            .WithData("ExamId", examId)
            .WithData("Operation", "exam-result");
    }

    private static string ComputeGrade(double obtained, double total, ICollection<GradeRange>? ranges = null, double scaleTotal = 100.0)
    {
        if (total <= 0) return "—";
        if (ranges?.Any() == true)
        {
            var scaledMarks = (obtained / total) * scaleTotal;
            var match = ranges
                .OrderByDescending(r => r.MinMarks)
                .FirstOrDefault(r => scaledMarks >= r.MinMarks && scaledMarks <= r.MaxMarks);
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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View)]
    public async Task<StudentResultViewDto> GetStudentResultDetailsAsync(Guid examId, Guid studentId)
    {
        var student = await studentRepository.GetAsync(studentId);
        var exam = await GetScopedExamAsync(examId, student.DivisionId);

        // Fetch standard and division names
        var standardName = student.StandardId.HasValue 
            ? (await standardRepository.FindAsync(student.StandardId.Value))?.Name ?? "—"
            : "—";
        var divisionName = student.DivisionId.HasValue
            ? (await divisionRepository.FindAsync(student.DivisionId.Value))?.Name ?? "—"
            : "—";

        // Load result detail for the student
        var header = await resultHeaderRepository.FirstOrDefaultAsync(
            h => h.ExamId == examId && h.DivisionId == student.DivisionId);
        
        ExamResultDetail? detail = null;
        int totalStudentsCount = 0;
        if (header != null)
        {
            detail = await resultDetailRepository.FirstOrDefaultAsync(
                d => d.ResultHeaderId == header.Id && d.StudentId == studentId);
            totalStudentsCount = (int)await resultDetailRepository.CountAsync(d => d.ResultHeaderId == header.Id);
        }

        // Attendance (pseudo-random rate but stable based on student RollNo or GrNo, same as frontend)
        var rollNoNum = student.RollNo ?? 1;
        var attendanceRate = 85 + (rollNoNum * 7) % 15;

        // Load marks
        var marks = await examMarkRepository.GetListAsync(
            m => m.ExamId == examId && m.StudentId == studentId);

        var applicableSubjectIdsByStudent = await subjectApplicabilityResolver.ResolveAsync(exam, [student]);
        var applicableSubjectIds = applicableSubjectIdsByStudent[student.Id];
        var applicableExamSubjects = exam.ExamSubjects
            .Where(subject => applicableSubjectIds.Contains(subject.SubjectId))
            .ToList();
        marks = marks.Where(mark => applicableSubjectIds.Contains(mark.SubjectId)).ToList();

        var subjectIds = applicableExamSubjects.Select(s => s.SubjectId).ToList();
        var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var subjectMap = subjects.ToDictionary(s => s.Id, s => s.Name);

        // Load default grading scale from the overall exam
        var defaultScaleId = exam.GradingScaleId;
        ICollection<GradeRange>? gradeRanges = null;
        double scaleTotal = 100.0;

        // Only the overall and applicable subject scales are needed for this student.
        var scaleIds = applicableExamSubjects
            .Where(subject => subject.GradeScalingId.HasValue)
            .Select(subject => subject.GradeScalingId!.Value)
            .ToHashSet();
        if (defaultScaleId.HasValue)
            scaleIds.Add(defaultScaleId.Value);
        var scaleQuery = await gradingScaleRepository.WithDetailsAsync(s => s.GradeRanges);
        var allScales = await AsyncExecuter.ToListAsync(scaleQuery.Where(scale => scaleIds.Contains(scale.Id)));
        var scaleMap = allScales.ToDictionary(
            s => s.Id,
            s => {
                double totalVal = 100.0;
                double.TryParse(s.TotalMarks, out totalVal);
                return (Ranges: s.GradeRanges, ScaleTotal: totalVal);
            }
        );

        if (defaultScaleId.HasValue && scaleMap.TryGetValue(defaultScaleId.Value, out var defScaleInfo))
        {
            gradeRanges = defScaleInfo.Ranges;
            scaleTotal = defScaleInfo.ScaleTotal;
        }

        var subjectMarks = new List<StudentSubjectMarkDto>();
        double totalMaxMarks = applicableExamSubjects.Sum(s => s.TotalMarks ?? 0);
        double totalObtainedMarks = 0;

        foreach (var examSub in applicableExamSubjects)
        {
            var mark = marks.FirstOrDefault(m => m.SubjectId == examSub.SubjectId);
            var subName = subjectMap.GetValueOrDefault(examSub.SubjectId, "—");
            var maxMarksVal = examSub.TotalMarks ?? 100;

            string marksObtainedStr = "—";
            string subjectGrade = "—";
            string subjectRemarks = "—";
            bool isSubjectPassed = false;

            if (mark != null)
            {
                if (mark.IsAbsent)
                {
                    marksObtainedStr = "Ab";
                    subjectRemarks = "Absent";
                }
                else
                {
                    var obtained = mark.MarksObtained ?? 0;
                    totalObtainedMarks += obtained;
                    marksObtainedStr = obtained.ToString("F2");

                    // Load specific grading scale for this subject if available
                    ICollection<GradeRange>? subRanges = null;
                    double subScaleTotal = 100.0;
                    if (examSub.GradeScalingId.HasValue && scaleMap.TryGetValue(examSub.GradeScalingId.Value, out var subScaleInfo))
                    {
                        subRanges = subScaleInfo.Ranges;
                        subScaleTotal = subScaleInfo.ScaleTotal;
                    }
                    else
                    {
                        subRanges = gradeRanges;
                        subScaleTotal = scaleTotal;
                    }

                    subjectGrade = ComputeGrade(obtained, maxMarksVal, subRanges, subScaleTotal);
                    subjectRemarks = GetRemarksFromGrade(subjectGrade);

                    var passingMarksVal = examSub.PassingMarks ?? (maxMarksVal * 0.33);
                    isSubjectPassed = obtained >= passingMarksVal;
                }
            }

            subjectMarks.Add(new StudentSubjectMarkDto
            {
                SubjectName = subName,
                MaxMarks = maxMarksVal.ToString("F2"),
                MarksObtained = marksObtainedStr,
                Grade = subjectGrade,
                Remarks = subjectRemarks,
                IsPassed = isSubjectPassed
            });
        }

        string overallPct = "—";
        string overallGrade = "—";
        string overallStatus = "—";
        int overallRank = 1;
        var displayTotalMaxMarks = totalMaxMarks;
        var displayTotalObtainedMarks = totalObtainedMarks;

        if (detail != null)
        {
            displayTotalMaxMarks = detail.TotalMaxMarks;
            displayTotalObtainedMarks = detail.TotalObtainedMarks;
            overallPct = $"{detail.Percentage}%";
            overallGrade = detail.Grade;
            overallStatus = detail.ResultStatus.ToString().ToUpper();
            overallRank = detail.ClassRank ?? 1;
        }
        else
        {
            // If result header not generated, compute overall dynamically
            var isAbsent = applicableExamSubjects.Count == 0 || marks.Count == 0 || marks.All(m => m.IsAbsent);
            if (!isAbsent && totalMaxMarks > 0)
            {
                var pct = Math.Round(totalObtainedMarks / totalMaxMarks * 100, 2);
                overallPct = $"{pct}%";
                overallGrade = ComputeGrade(totalObtainedMarks, totalMaxMarks, gradeRanges, scaleTotal);

                var failedAnySubject = applicableExamSubjects.Any(s =>
                {
                    var mark = marks.FirstOrDefault(m => m.SubjectId == s.SubjectId);
                    if (mark == null || mark.IsAbsent) return true;
                    var passingMarksVal = s.PassingMarks ?? ((s.TotalMarks ?? 100.0) * 0.33);
                    return (mark.MarksObtained ?? 0) < passingMarksVal;
                });

                overallStatus = failedAnySubject ? "FAIL" : "PASS";
            }
        }

        return new StudentResultViewDto
        {
            StudentId = studentId,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            GrNo = student.GrNo?.ToString() ?? "—",
            RollNo = student.RollNo?.ToString() ?? "—",
            DateOfBirth = student.AdditionalDetail?.DateOfBirth?.ToString("dd MMM yyyy") ?? "—",
            ClassName = standardName,
            DivisionName = divisionName,
            ExamName = exam.ExamName,
            Grade = overallGrade,
            Rank = overallRank,
            TotalStudentsInClass = totalStudentsCount > 0 ? totalStudentsCount : 1,
            TotalMarks = displayTotalMaxMarks.ToString("F2"),
            ObtainedMarks = displayTotalObtainedMarks.ToString("F2"),
            Percentage = overallPct,
            AttendancePercentage = $"{attendanceRate}%",
            Status = overallStatus,
            Subjects = subjectMarks
        };
    }

    private sealed record ResultInputs(
        List<Student> EnrolledStudents,
        Dictionary<Guid, HashSet<Guid>> ApplicableSubjectIdsByStudent,
        List<ExamMark> Marks);

    private static string GetRemarksFromGrade(string grade)
    {
        return grade switch
        {
            "A+" => "Excellent",
            "A" => "Very Good",
            "B+" => "Good",
            "B" => "Satisfactory",
            "C+" => "Fair",
            "C" => "Satisfactory",
            "D" => "Needs Improvement",
            "F" => "Unsatisfactory",
            _ => "—"
        };
    }
}
