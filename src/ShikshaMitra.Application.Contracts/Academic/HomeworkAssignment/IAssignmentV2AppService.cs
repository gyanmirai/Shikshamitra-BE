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

    Task<AssignmentV2ResponseDto> GetAsync(Guid assignmentId, Guid? academicMediumId = null);

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
}
