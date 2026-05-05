using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public interface IStudentSubmissionAppService : IApplicationService
{
    Task<SubmissionUploadResponseDto> UploadHomeworkSubmissionAsync(
        UploadHomeworkSubmissionDto input,
        string blobName,
        string? originalFileName
    );

    Task<SubmissionUploadResponseDto> UploadAssignmentSubmissionAsync(
        UploadAssignmentSubmissionDto input,
        string blobName,
        string? originalFileName
    );

    Task<AssessmentListDto<HomeworkAssessmentItemDto>> GetHomeworkAssessmentsAsync(
        Guid homeworkId,
        GetAssessmentsInput input
    );

    Task<AssessmentListDto<AssignmentAssessmentItemDto>> GetAssignmentAssessmentsAsync(
        Guid assignmentId,
        GetAssessmentsInput input
    );
}
