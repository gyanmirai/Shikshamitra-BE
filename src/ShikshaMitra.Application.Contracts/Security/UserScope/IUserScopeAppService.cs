using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Application service interface for user scope management.
/// </summary>
public interface IUserScopeAppService : IApplicationService
{
    /// <summary>
    /// Assigns a scope to a user or role.
    /// </summary>
    /// <param name="input">Scope assignment details</param>
    /// <returns>Created scope assignment</returns>
    Task<UserScopeAssignmentDto> AssignScopeAsync(CreateUserScopeAssignmentDto input);

    /// <summary>
    /// Bulk assigns scope to multiple users.
    /// Creates multiple scope assignments in a single operation.
    /// </summary>
    /// <param name="input">Bulk assignment details</param>
    /// <returns>Number of assignments created</returns>
    Task<int> BulkAssignAsync(BulkScopeAssignmentDto input);

    /// <summary>
    /// Revokes a specific scope assignment.
    /// </summary>
    /// <param name="id">Scope assignment ID</param>
    Task RevokeScopeAsync(Guid id);

    /// <summary>
    /// Revokes all scope assignments for a specific user.
    /// </summary>
    /// <param name="userId">User ID</param>
    Task RevokeUserScopesAsync(Guid userId);

    /// <summary>
    /// Gets the complete scope information for a user.
    /// Includes all assignments and effective scope.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User scope details</returns>
    Task<UserScopeDto> GetUserScopeAsync(Guid userId);

    /// <summary>
    /// Gets all users who have access to a specific division.
    /// </summary>
    /// <param name="divisionId">Division ID</param>
    /// <returns>List of user IDs with access</returns>
    Task<List<Guid>> GetUsersByDivisionAsync(Guid divisionId);

    /// <summary>
    /// Gets all users who have access to a specific standard/class.
    /// </summary>
    /// <param name="standardId">Standard/Class ID</param>
    /// <returns>List of user IDs with access</returns>
    Task<List<Guid>> GetUsersByStandardAsync(Guid standardId);

    /// <summary>
    /// Gets all scope assignments (for admin panel).
    /// </summary>
    /// <param name="userId">Optional: Filter by user ID</param>
    /// <param name="roleName">Optional: Filter by role name</param>
    /// <returns>List of scope assignments</returns>
    Task<List<UserScopeAssignmentDto>> GetAllAssignmentsAsync(Guid? userId = null, string? roleName = null);

    /// <summary>
    /// Activates a scope assignment.
    /// </summary>
    /// <param name="id">Scope assignment ID</param>
    Task ActivateScopeAsync(Guid id);

    /// <summary>
    /// Deactivates a scope assignment (soft disable without deleting).
    /// </summary>
    /// <param name="id">Scope assignment ID</param>
    Task DeactivateScopeAsync(Guid id);

    /// <summary>
    /// Updates the description of a scope assignment.
    /// </summary>
    /// <param name="id">Scope assignment ID</param>
    /// <param name="description">New description</param>
    Task UpdateDescriptionAsync(Guid id, string description);
}
