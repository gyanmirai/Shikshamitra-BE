using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Groups;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Groups;

/// <summary>
/// API controller for Group management (audience targeting for Holidays/Events)
/// </summary>
[Authorize]
[Route("api/groups")]
public class GroupController(IGroupAppService groupAppService) : AbpController
{
    #region CRUD Operations

    /// <summary>
    /// Get paginated list of groups with optional filters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<GroupDto>), StatusCodes.Status200OK)]
    public async Task<PagedResultDto<GroupDto>> GetListAsync([FromQuery] GetGroupListRequestDto input)
    {
        return await groupAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get a group by ID with full details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<GroupDto> GetAsync(Guid id)
    {
        return await groupAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new group (Rule-Based or Name-Based)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateUpdateGroupDto input)
    {
        var group = await groupAppService.CreateAsync(academicYearId, input);
        return StatusCode(StatusCodes.Status201Created, group);
    }

    /// <summary>
    /// Update an existing group
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<GroupDto> UpdateAsync(Guid id, [FromQuery] Guid academicYearId, [FromBody] CreateUpdateGroupDto input)
    {
        return await groupAppService.UpdateAsync(id, academicYearId, input);
    }

    /// <summary>
    /// Delete a group
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await groupAppService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region User Selection for Name-Based Groups

    /// <summary>
    /// Get paginated list of users (students or employees) for selection in Name-Based groups
    /// </summary>
    [HttpPost("users-for-selection")]
    [ProducesResponseType(typeof(PagedResultDto<UserSelectionDto>), StatusCodes.Status200OK)]
    public async Task<PagedResultDto<UserSelectionDto>> GetUsersForSelectionAsync([FromBody] GetUsersForSelectionDto input)
    {
        return await groupAppService.GetUsersForSelectionAsync(input);
    }

    #endregion

    #region Member Resolution

    /// <summary>
    /// Get all resolved member user IDs for a group (evaluates rules for Rule-Based groups)
    /// </summary>
    [HttpGet("{id}/members")]
    [ProducesResponseType(typeof(List<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<List<Guid>> GetGroupMembersAsync(Guid id)
    {
        return await groupAppService.GetGroupMembersAsync(id);
    }

    /// <summary>
    /// Get the count of members in a group
    /// </summary>
    [HttpGet("{id}/member-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<int> GetGroupMemberCountAsync(Guid id)
    {
        return await groupAppService.GetGroupMemberCountAsync(id);
    }

    /// <summary>
    /// Check if a user belongs to any of the specified groups
    /// </summary>
    [HttpPost("is-user-in-groups")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<bool> IsUserInGroupsAsync([FromBody] IsUserInGroupsRequestDto request)
    {
        return await groupAppService.IsUserInGroupsAsync(request.UserId, request.GroupIds);
    }

    #endregion
}

/// <summary>
/// Request DTO for checking if a user belongs to groups
/// </summary>
public class IsUserInGroupsRequestDto
{
    public Guid UserId { get; set; }
    public List<Guid> GroupIds { get; set; } = new();
}
