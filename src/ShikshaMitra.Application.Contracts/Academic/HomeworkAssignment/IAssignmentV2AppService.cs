using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public interface IAssignmentV2AppService : IApplicationService
{
    Task<AssignmentV2CreateResponseDto> CreateAsync(
        Guid academicMediumId,
        CreateAssignmentV2Dto input,
        string? attachmentBlobName
    );

    Task<AssignmentV2ListResponseDto> GetListAsync(
        Guid academicMediumId,
        GetAssignmentV2ListFilterDto filter
    );

    Task<AssignmentV2ResponseDto> GetAsync(Guid id, Guid? academicMediumId = null);

    Task<AssignmentV2ResponseDto> UpdateAsync(
        Guid assignmentId,
        UpdateAssignmentV2Dto input,
        string? attachmentBlobName
    );

    Task DeleteAsync(Guid assignmentId);

    Task<AssignmentStatusUpdateResponseDto> ChangeStatusAsync(
        Guid assignmentId,
        AssignmentStatusUpdateDto input
    );

    Task<LearningTaskDashboardDto> GetDashboardAsync(
        Guid academicMediumId,
        LearningTaskDashboardFilterDto input
    );

    Task<LearningTaskReminderResponseDto> SendReminderAsync(
        Guid assignmentId,
        LearningTaskReminderRequestDto input
    );

    Task<LearningTaskHistoryDto> GetHistoryAsync(Guid assignmentId);
}
