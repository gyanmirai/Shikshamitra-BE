using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Security.UserScope;

/// <summary>
/// EF Core implementation of IIdentityUserRepositoryExtensions.
/// Provides efficient database-level queries for user-role relationships.
/// </summary>
public class EfCoreIdentityUserRepositoryExtensions : IIdentityUserRepositoryExtensions
{
    private readonly IDbContextProvider<ShikshaMitraDbContext> _dbContextProvider;

    public EfCoreIdentityUserRepositoryExtensions(
        IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<List<Guid>> GetUserIdsByRoleIdAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();

        // Query at database level: join Users with UserRoles to get user IDs with the specified role
        var userIds = await dbContext.Set<IdentityUser>()
            .Where(u => u.Roles.Any(r => r.RoleId == roleId))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return userIds;
    }
}
