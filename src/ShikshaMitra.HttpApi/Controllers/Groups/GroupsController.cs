using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.Groups.Dtos;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Groups;

[Route("groups")]
public class GroupsController : ShikshaMitraController
{
    private readonly IGroupAppService _groupAppService;

    public GroupsController(IGroupAppService groupAppService)
    {
        _groupAppService = groupAppService;
    }

    [HttpGet]
    public async Task<GroupTableResponseDto> GetListAsync(
        [FromQuery] int currentPage = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = "asc")
    {
        var input = new GetGroupsInputDto
        {
            Search = search,
            Sorting = !string.IsNullOrEmpty(sortBy) ? $"{sortBy} {sortOrder}" : null,
            SkipCount = (currentPage - 1) * pageSize,
            MaxResultCount = pageSize
        };

        var result = await _groupAppService.GetListAsync(input);

        return new GroupTableResponseDto
        {
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize),
            Items = result.Items.Select(x => new GroupTableRowDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                TotalAudience = x.TotalAudience,
                Students = x.Students,
                Employees = x.Employees,
                CreatedAt = x.CreatedAt
            }).ToList()
        };
    }

    [HttpPost]
    public async Task<GroupDto> CreateAsync(CreateGroupDto input)
    {
        return await _groupAppService.CreateAsync(input);
    }

    [HttpPost("preview")]
    public async Task<GroupPreviewResultDto> PreviewAsync(GroupPreviewInputDto input)
    {
        return await _groupAppService.PreviewAsync(input);
    }

    [HttpGet("{groupId}")]
    public async Task<GroupDetailsResponseDto> GetAsync(Guid groupId)
    {
        return await _groupAppService.GetDetailedAsync(groupId);
    }

    [HttpPut("{groupId}")]
    public async Task<GroupDto> UpdateAsync(Guid groupId, UpdateGroupDto input)
    {
        return await _groupAppService.UpdateAsync(groupId, input);
    }

    [HttpDelete("{groupId}")]
    public async Task DeleteAsync(Guid groupId)
    {
        await _groupAppService.DeleteAsync(groupId);
    }
}
