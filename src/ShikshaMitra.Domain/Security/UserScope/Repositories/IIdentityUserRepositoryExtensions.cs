using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Extension methods for IIdentityUserRepository to support efficient user queries by role.
/// </summary>
public interface IIdentityUserRepositoryExtensions
{
    /// <summary>
    /// Gets user IDs for all users that have the specified role.
    /// This method performs a database-level query and does not load full user entities into memory.
    /// </summary>
    /// <param name="roleId">The role ID to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user IDs that have the specified role</returns>
    Task<List<Guid>> GetUserIdsByRoleIdAsync(
        Guid roleId,
        CancellationToken cancellationToken = default
    );
}
