using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Authorize]
[RemoteService(IsEnabled = false)]
public class StudentSubmissionAppService(
    IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
    IRepository<AssignmentEntity, Guid> assignmentRepository,
    IStudentSubmissionRepository submissionRepository,
    StudentSubmissionManager submissionManager,
    IStudentRepository studentRepository,
    IAsyncQueryableExecuter asyncExecuter
) : ApplicationService, IStudentSubmissionAppService
{
    public async Task<SubmissionUploadResponseDto> UploadHomeworkSubmissionAsync(
        UploadHomeworkSubmissionDto input,
        string blobName,
        string? originalFileName
    )
    {
        var q = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(q.Where(h => h.Id == input.HomeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound")
                .WithData("HomeworkId", input.HomeworkId);

        if (homework.SubmissionType != SubmissionType.FileUpload)
            throw new BusinessException("ShikshaMitra.Homework:InvalidSubmissionType")
                .WithData("Expected", SubmissionType.FileUpload)
                .WithData("Actual", homework.SubmissionType);

        EnsureHomeworkOpenForSubmission(homework);
        var student = await studentRepository.GetAsync(input.StudentId);
        var divisionId = student.DivisionId
            ?? throw new BusinessException("ShikshaMitra.Homework:StudentDivisionRequired")
                .WithData("StudentId", input.StudentId);
        var schedule = homework.DivisionSchedules.FirstOrDefault(s => s.DivisionId == divisionId);
        if (homework.DivisionSchedules.Any() && schedule == null)
        {
            throw new BusinessException("ShikshaMitra.Homework:StudentNotInAssignedDivision")
                .WithData("StudentId", input.StudentId)
                .WithData("DivisionId", divisionId);
        }

        var dueDate = schedule?.DueDate ?? homework.DueDate ?? DateTime.UtcNow.AddDays(7);

        var submission = await submissionManager.CreateOrGetSubmissionAsync(
            input.HomeworkId, input.StudentId, divisionId, dueDate, CurrentTenant.Id);
        EnsureStudentCanResubmit(submission);

        submission.SubmittedDocumentUrl = blobName;
        submission.OriginalFileName = originalFileName;
        submission.SubmissionDate = DateTimeOffset.UtcNow;
        submission.Status = DateTime.UtcNow > dueDate ? SubmissionStatus.Late : SubmissionStatus.Submitted;

        await submissionRepository.UpdateAsync(submission);

        return new SubmissionUploadResponseDto
        {
            SubmissionId = submission.Id,
            StudentId = input.StudentId,
            ReferenceId = input.HomeworkId,
            FileUrl = blobName,
            SubmittedAt = submission.SubmissionDate.Value,
            Status = submission.Status
        };
    }

    public async Task<SubmissionUploadResponseDto> UploadAssignmentSubmissionAsync(
        UploadAssignmentSubmissionDto input,
        string blobName,
        string? originalFileName
    )
    {
        var q = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(q.Where(a => a.Id == input.AssignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound")
                .WithData("AssignmentId", input.AssignmentId);

        if (assignment.SubmissionType != SubmissionType.FileUpload)
            throw new BusinessException("ShikshaMitra.Assignment:InvalidSubmissionType")
                .WithData("Expected", SubmissionType.FileUpload)
                .WithData("Actual", assignment.SubmissionType);

        EnsureAssignmentOpenForSubmission(assignment);
        var student = await studentRepository.GetAsync(input.StudentId);
        var divisionId = student.DivisionId
            ?? throw new BusinessException("ShikshaMitra.Assignment:StudentDivisionRequired")
                .WithData("StudentId", input.StudentId);
        var schedule = assignment.DivisionSchedules.FirstOrDefault(s => s.DivisionId == divisionId)
            ?? throw new BusinessException("ShikshaMitra.Assignment:StudentNotInAssignedDivision")
                .WithData("StudentId", input.StudentId)
                .WithData("DivisionId", divisionId);
        var dueDate = schedule.DueDate;

        var submission = await submissionManager.CreateOrGetSubmissionForAssignmentAsync(
            input.AssignmentId, input.StudentId, divisionId, dueDate, CurrentTenant.Id);
        EnsureStudentCanResubmit(submission);

        submission.SubmittedDocumentUrl = blobName;
        submission.OriginalFileName = originalFileName;
        submission.SubmissionDate = DateTimeOffset.UtcNow;
        submission.Status = DateTime.UtcNow > dueDate ? SubmissionStatus.Late : SubmissionStatus.Submitted;

        await submissionRepository.UpdateAsync(submission);

        return new SubmissionUploadResponseDto
        {
            SubmissionId = submission.Id,
            StudentId = input.StudentId,
            ReferenceId = input.AssignmentId,
            FileUrl = blobName,
            SubmittedAt = submission.SubmissionDate.Value,
            Status = submission.Status
        };
    }

    public async Task<AssessmentListDto<HomeworkAssessmentItemDto>> GetHomeworkAssessmentsAsync(
        Guid homeworkId,
        GetAssessmentsInput input)
    {
        var q = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(q.Where(h => h.Id == homeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound")
                .WithData("HomeworkId", homeworkId);

        var schedule = homework.DivisionSchedules.FirstOrDefault(s => s.DivisionId == input.DivisionId);
        var dueAt = schedule?.DueDate ?? homework.DueDate ?? DateTime.UtcNow;

        var students = await studentRepository.GetListAsync(
            s => s.DivisionId == input.DivisionId && s.StandardId == input.StandardId);

        var submissions = await submissionRepository.GetSubmissionsWithDetailsAsync(homeworkId, input.DivisionId);
        var submissionDict = submissions
            .GroupBy(s => s.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SubmissionDate).First());

        var items = students.Select(student =>
        {
            submissionDict.TryGetValue(student.Id, out var sub);
            return new HomeworkAssessmentItemDto
            {
                Id = sub?.Id,
                StudentId = student.Id,
                StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                    .Where(n => !string.IsNullOrWhiteSpace(n))),
                GrNumber = student.GrNo?.ToString(),
                RollNo = student.RollNo,
                SubmissionType = homework.SubmissionType,
                SubmissionAt = sub?.SubmissionDate,
                DueAt = dueAt,
                Feedback = sub?.Feedback,
                Status = sub?.Status ?? SubmissionStatus.NotSubmitted,
                CreatedAt = sub?.CreationTime,
                UpdatedAt = sub?.LastModificationTime,
                Attachment = sub?.SubmittedDocumentUrl is { } url
                    ? new AttachmentInfoDto
                    {
                        FileName = sub.OriginalFileName ?? Path.GetFileName(url),
                        FileUrl = url,
                        UploadedAt = sub.SubmissionDate
                    }
                    : null
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim().ToLowerInvariant();
            items = items.Where(i =>
                i.StudentName.ToLowerInvariant().Contains(search) ||
                (i.RollNo?.ToString().Contains(search) ?? false)).ToList();
        }

        if (input.Status.HasValue)
            items = items.Where(i => i.Status == input.Status.Value).ToList();

        var totalCount = items.Count;
        var pagedItems = items
            .Skip((input.Page - 1) * input.PageSize)
            .Take(input.PageSize)
            .ToList();

        return new AssessmentListDto<HomeworkAssessmentItemDto>
        {
            TotalCount = totalCount,
            CurrentPage = input.Page,
            PageSize = input.PageSize,
            TotalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize),
            Items = pagedItems
        };
    }

    public async Task<AssessmentListDto<AssignmentAssessmentItemDto>> GetAssignmentAssessmentsAsync(
        Guid assignmentId,
        GetAssessmentsInput input)
    {
        var q = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(q.Where(a => a.Id == assignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound")
                .WithData("AssignmentId", assignmentId);

        var schedule = assignment.DivisionSchedules.FirstOrDefault(s => s.DivisionId == input.DivisionId);
        var dueAt = schedule?.DueDate ?? DateTime.UtcNow;

        var students = await studentRepository.GetListAsync(
            s => s.DivisionId == input.DivisionId && s.StandardId == input.StandardId);

        var submissions = await submissionRepository.GetSubmissionsByAssignmentAndDivisionAsync(assignmentId, input.DivisionId);
        var submissionDict = submissions
            .GroupBy(s => s.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SubmissionDate).First());

        var items = students.Select(student =>
        {
            submissionDict.TryGetValue(student.Id, out var sub);
            decimal? percentage = sub?.Marks.HasValue == true && assignment.TotalMarks > 0
                ? Math.Round(sub.Marks!.Value / assignment.TotalMarks * 100, 2)
                : null;

            return new AssignmentAssessmentItemDto
            {
                Id = sub?.Id,
                StudentId = student.Id,
                StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                    .Where(n => !string.IsNullOrWhiteSpace(n))),
                GrNumber = student.GrNo?.ToString(),
                RollNo = student.RollNo,
                SubmissionType = assignment.SubmissionType,
                SubmissionAt = sub?.SubmissionDate,
                DueAt = dueAt,
                TotalMarks = assignment.TotalMarks,
                ObtainedMarks = sub?.Marks,
                Percentage = percentage,
                Grade = null,
                Feedback = sub?.Feedback,
                Status = sub?.Status ?? SubmissionStatus.NotSubmitted,
                CreatedAt = sub?.CreationTime,
                UpdatedAt = sub?.LastModificationTime,
                Attachment = sub?.SubmittedDocumentUrl is { } url
                    ? new AttachmentInfoDto
                    {
                        FileName = sub.OriginalFileName ?? Path.GetFileName(url),
                        FileUrl = url,
                        UploadedAt = sub.SubmissionDate
                    }
                    : null
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim().ToLowerInvariant();
            items = items.Where(i =>
                i.StudentName.ToLowerInvariant().Contains(search) ||
                (i.RollNo?.ToString().Contains(search) ?? false)).ToList();
        }

        if (input.Status.HasValue)
            items = items.Where(i => i.Status == input.Status.Value).ToList();

        var totalCount = items.Count;
        var pagedItems = items
            .Skip((input.Page - 1) * input.PageSize)
            .Take(input.PageSize)
            .ToList();

        return new AssessmentListDto<AssignmentAssessmentItemDto>
        {
            TotalCount = totalCount,
            CurrentPage = input.Page,
            PageSize = input.PageSize,
            TotalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize),
            Items = pagedItems
        };
    }

    private static void EnsureHomeworkOpenForSubmission(HomeworkAssignmentEntity homework)
    {
        var published = homework.Status == HomeworkStatus.Published || homework.IsPublished || homework.HasPublishedDivisions;
        if (!published)
        {
            throw new BusinessException("ShikshaMitra.Homework:NotPublished");
        }

        if (homework.Status == HomeworkStatus.Closed)
        {
            throw new BusinessException("ShikshaMitra.Homework:Closed");
        }
    }

    private static void EnsureAssignmentOpenForSubmission(AssignmentEntity assignment)
    {
        var published = assignment.Status == AssignmentStatus.Published || assignment.HasPublishedDivisions;
        if (!published)
        {
            throw new BusinessException("ShikshaMitra.Assignment:NotPublished");
        }

        if (assignment.Status == AssignmentStatus.Closed)
        {
            throw new BusinessException("ShikshaMitra.Assignment:Closed");
        }
    }

    private static void EnsureStudentCanResubmit(StudentSubmissionEntity submission)
    {
        if (submission.Status == SubmissionStatus.Reviewed)
        {
            throw new BusinessException("ShikshaMitra.Submission:ReviewedSubmissionLocked")
                .WithData("SubmissionId", submission.Id);
        }

        if (submission.Status == SubmissionStatus.Excused)
        {
            throw new BusinessException("ShikshaMitra.Submission:ExcusedSubmissionLocked")
                .WithData("SubmissionId", submission.Id);
        }
    }
}

