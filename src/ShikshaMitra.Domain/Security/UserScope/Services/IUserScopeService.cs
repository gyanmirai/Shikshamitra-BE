using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Domain service for user scope operations.
/// Handles scope resolution, caching, and validation.
/// </summary>
public interface IUserScopeService
{
    /// <summary>
    /// Gets the effective scope for a user (combines user-specific and role-based assignments).
    /// Results are cached for performance.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>UserScopeContext with all accessible entity IDs</returns>
    Task<UserScopeContext> GetUserScopeAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidates the cached scope for a user.
    /// Should be called whenever a user's scope assignments change.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateUserScopeCacheAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidates the cached scope for all users with a specific role.
    /// Should be called whenever role-based scope assignments change.
    /// </summary>
    /// <param name="roleName">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateRoleScopeCacheAsync(
        string roleName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a user is an administrator (bypasses all scope checks).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user is admin, false otherwise</returns>
    Task<bool> IsAdminUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
}
