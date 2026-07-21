using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[RemoteService(IsEnabled = false)]
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

    Task<LearningTaskDashboardDto> GetDashboardAsync(
        Guid academicMediumId,
        LearningTaskDashboardFilterDto input
    );

    Task<LearningTaskReminderResponseDto> SendReminderAsync(
        Guid homeworkId,
        LearningTaskReminderRequestDto input
    );

    Task<LearningTaskHistoryDto> GetHistoryAsync(Guid homeworkId);
}
