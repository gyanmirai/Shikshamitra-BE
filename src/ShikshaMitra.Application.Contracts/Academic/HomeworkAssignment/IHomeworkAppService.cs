using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public interface IHomeworkAppService : IApplicationService
{
    Task<HomeworkCreateResponseDto> CreateAsync(
        Guid academicMediumId,
        CreateHomeworkDto input,
        string? attachmentBlobName
    );

    Task<HomeworkListResponseDto> GetListAsync(
        Guid academicMediumId,
        GetHomeworkListFilterDto filter
    );

    Task<HomeworkResponseDto> GetAsync(Guid homeworkId, Guid? academicMediumId = null);

    Task<HomeworkResponseDto> UpdateAsync(
        Guid homeworkId,
        UpdateHomeworkDto input,
        string? attachmentBlobName
    );

    Task DeleteAsync(Guid homeworkId);

    Task<HomeworkStatusUpdateResponseDto> ChangeStatusAsync(
        Guid homeworkId,
        HomeworkStatusUpdateDto input
    );
}
