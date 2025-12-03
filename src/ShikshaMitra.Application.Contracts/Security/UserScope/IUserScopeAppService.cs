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
    /// Bulk assigns scope to multiple users with support for multiple boards, standards, etc.
    /// Creates assignments for all combinations (cartesian product) of the provided scope parameters.
    /// For example: 2 users × 3 boards × 2 standards = 12 assignments.
    /// 
    /// IMPORTANT - OVERWRITE STRATEGY:
    /// This operation first DELETES all existing scope assignments for the specified users,
    /// then creates the new assignments. This is NOT an append operation.
    /// 
    /// The entire operation is transactional - either all operations (delete + create) succeed or none are applied.
    /// </summary>
    /// <param name="input">Bulk assignment details with lists for each scope level</param>
    /// <returns>Result containing deleted count, created count, and affected users</returns>
    Task<BulkScopeAssignmentResultDto> BulkAssignAsync(BulkScopeAssignmentDto input);

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
