using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public interface IStudentSubmissionAppService : IApplicationService
{
    Task<SubmissionUploadResponseDto> UploadHomeworkSubmissionAsync(
        UploadHomeworkSubmissionDto input,
        string blobName
    );

    Task<SubmissionUploadResponseDto> UploadAssignmentSubmissionAsync(
        UploadAssignmentSubmissionDto input,
        string blobName
    );
}
