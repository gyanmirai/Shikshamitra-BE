using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Groups.Dtos;

namespace ShikshaMitra.Groups;

public interface IGroupAppService : IApplicationService
{
    Task<PagedResultDto<GroupDto>> GetListAsync(GetGroupsInputDto input);
    Task<GroupDetailsDto> GetAsync(Guid id);
    Task<GroupDto> CreateAsync(CreateGroupDto input);
    Task<GroupDto> UpdateAsync(Guid id, UpdateGroupDto input);
    Task<GroupDetailsResponseDto> GetDetailedAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<GroupPreviewResultDto> PreviewAsync(GroupPreviewInputDto input);
}
