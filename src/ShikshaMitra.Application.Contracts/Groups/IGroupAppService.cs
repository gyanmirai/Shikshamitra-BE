using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// Application service interface for Group management
/// </summary>
public interface IGroupAppService : IApplicationService
{
    /// <summary>
    /// Gets a group by ID with full details
    /// </summary>
    Task<GroupDto> GetAsync(Guid id);

    /// <summary>
    /// Gets paginated list of groups with filters
    /// </summary>
    Task<PagedResultDto<GroupDto>> GetListAsync(GetGroupListRequestDto input);

    /// <summary>
    /// Creates a new group
    /// </summary>
    /// <param name="academicYearId">Academic Year ID from query parameter</param>
    /// <param name="input">Group data</param>
    Task<GroupDto> CreateAsync(Guid academicYearId, CreateUpdateGroupDto input);

    /// <summary>
    /// Updates an existing group
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <param name="academicYearId">Academic Year ID from query parameter</param>
    /// <param name="input">Updated group data</param>
    Task<GroupDto> UpdateAsync(Guid id, Guid academicYearId, CreateUpdateGroupDto input);

    /// <summary>
    /// Deletes a group
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Gets paginated list of users (students or employees) for name-based group selection
    /// </summary>
    Task<PagedResultDto<UserSelectionDto>> GetUsersForSelectionAsync(GetUsersForSelectionDto input);

    /// <summary>
    /// Resolves and returns all member user IDs for a group
    /// </summary>
    Task<List<Guid>> GetGroupMembersAsync(Guid groupId);

    /// <summary>
    /// Checks if a user belongs to any of the specified groups
    /// </summary>
    Task<bool> IsUserInGroupsAsync(Guid userId, List<Guid> groupIds);

    /// <summary>
    /// Gets the member count for a group
    /// </summary>
    Task<int> GetGroupMemberCountAsync(Guid groupId);
}
