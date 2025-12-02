using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Repository interface for UserScopeAssignment entity.
/// Provides specialized queries for scope resolution.
/// </summary>
public interface IUserScopeAssignmentRepository : IRepository<UserScopeAssignment, Guid>
{
    /// <summary>
    /// Gets all active scope assignments for a specific user (both user-specific and role-based).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active scope assignments</returns>
    Task<List<UserScopeAssignment>> GetUserAssignmentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all active scope assignments for a specific role.
    /// </summary>
    /// <param name="roleName">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active scope assignments</returns>
    Task<List<UserScopeAssignment>> GetRoleAssignmentsAsync(
        string roleName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all users who have access to a specific division.
    /// </summary>
    /// <param name="divisionId">Division ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user IDs with access to the division</returns>
    Task<List<Guid>> GetUserIdsByDivisionAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all users who have access to a specific standard/class.
    /// </summary>
    /// <param name="standardId">Standard/Class ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user IDs with access to the standard</returns>
    Task<List<Guid>> GetUserIdsByStandardAsync(
        Guid standardId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a specific scope assignment already exists (prevents duplicates).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleName">Role name</param>
    /// <param name="academicYearId">Academic year ID</param>
    /// <param name="educationBoardId">Board ID</param>
    /// <param name="educationMediumId">Medium ID</param>
    /// <param name="educationMediumStageId">Stage ID</param>
    /// <param name="standardId">Standard ID</param>
    /// <param name="divisionId">Division ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if assignment exists, false otherwise</returns>
    Task<bool> ExistsAsync(
        Guid? userId,
        string? roleName,
        Guid? academicYearId,
        Guid? educationBoardId,
        Guid? educationMediumId,
        Guid? educationMediumStageId,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes all scope assignments for a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteUserAssignmentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes all scope assignments for a role.
    /// </summary>
    /// <param name="roleName">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteRoleAssignmentsAsync(
        string roleName,
        CancellationToken cancellationToken = default
    );
}
