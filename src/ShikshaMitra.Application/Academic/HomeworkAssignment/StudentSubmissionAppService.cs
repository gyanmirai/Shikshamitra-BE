using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    IAsyncQueryableExecuter asyncExecuter
) : ApplicationService, IStudentSubmissionAppService
{
    public async Task<SubmissionUploadResponseDto> UploadHomeworkSubmissionAsync(
        UploadHomeworkSubmissionDto input,
        string blobName
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

        var schedule = homework.DivisionSchedules.OrderBy(s => s.DueDate).FirstOrDefault();
        var dueDate = schedule?.DueDate ?? DateTime.UtcNow.AddDays(7);
        var divisionId = schedule?.DivisionId ?? Guid.Empty;

        var submission = await submissionManager.CreateOrGetSubmissionAsync(
            input.HomeworkId, input.StudentId, divisionId, dueDate, CurrentTenant.Id);

        submission.SubmittedDocumentUrl = blobName;
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
        string blobName
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

        var schedule = assignment.DivisionSchedules.OrderBy(s => s.DueDate).FirstOrDefault();
        var dueDate = schedule?.DueDate ?? DateTime.UtcNow.AddDays(7);
        var divisionId = schedule?.DivisionId ?? Guid.Empty;

        var submission = await submissionManager.CreateOrGetSubmissionForAssignmentAsync(
            input.AssignmentId, input.StudentId, divisionId, dueDate, CurrentTenant.Id);

        submission.SubmittedDocumentUrl = blobName;
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
}

