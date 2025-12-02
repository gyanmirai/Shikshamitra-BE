using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Security.UserScope;

/// <summary>
/// EF Core repository implementation for UserScopeAssignment.
/// </summary>
public class EfCoreUserScopeAssignmentRepository :
    EfCoreRepository<ShikshaMitraDbContext, UserScopeAssignment, Guid>,
    IUserScopeAssignmentRepository
{
    public EfCoreUserScopeAssignmentRepository(
        IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<UserScopeAssignment>> GetUserAssignmentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        // Single query to get user's role names
        var userRoleNames = await (
            from user in dbContext.Set<IdentityUser>()
            where user.Id == userId
            from userRole in user.Roles
            join role in dbContext.Set<IdentityRole>() on userRole.RoleId equals role.Id
            select role.Name
        ).ToListAsync(cancellationToken);

        // Single query to get both direct user assignments and role-based assignments
        var allAssignments = await (await GetQueryableAsync())
            .Where(x =>
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Direct user assignments
                    (x.UserId.HasValue && x.UserId.Value == userId) ||
                    // Role-based assignments
                    (x.RoleName != null && userRoleNames.Contains(x.RoleName))
                )
            )
            .ToListAsync(cancellationToken);

        return allAssignments;
    }

    public async Task<List<UserScopeAssignment>> GetRoleAssignmentsAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(x => x.RoleName == roleName && x.IsActive && !x.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetUserIdsByDivisionAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default)
    {
        return await GetUserIdsByFieldAsync(
            x => x.DivisionId,
            divisionId,
            cancellationToken);
    }

    public async Task<List<Guid>> GetUserIdsByStandardAsync(
        Guid standardId,
        CancellationToken cancellationToken = default)
    {
        return await GetUserIdsByFieldAsync(
            x => x.StandardId,
            standardId,
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid? userId,
        string? roleName,
        Guid? academicYearId,
        Guid? educationBoardId,
        Guid? educationMediumId,
        Guid? educationMediumStageId,
        Guid? standardId,
        Guid? divisionId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .AnyAsync(x =>
                x.UserId == userId &&
                x.RoleName == roleName &&
                (!x.AcademicYearId.HasValue && !academicYearId.HasValue || x.AcademicYearId == academicYearId) &&
                (!x.EducationBoardId.HasValue && !educationBoardId.HasValue || x.EducationBoardId == educationBoardId) &&
                (!x.EducationMediumId.HasValue && !educationMediumId.HasValue || x.EducationMediumId == educationMediumId) &&
                (!x.EducationMediumStageId.HasValue && !educationMediumStageId.HasValue || x.EducationMediumStageId == educationMediumStageId) &&
                (!x.StandardId.HasValue && !standardId.HasValue || x.StandardId == standardId) &&
                (!x.DivisionId.HasValue && !divisionId.HasValue || x.DivisionId == divisionId) &&
                !x.IsDeleted,
                cancellationToken);
    }

    public async Task DeleteUserAssignmentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await (await GetQueryableAsync())
            .Where(x => x.UserId.HasValue && x.UserId.Value == userId)
            .ToListAsync(cancellationToken);

        await DeleteManyAsync(assignments, cancellationToken: cancellationToken);
    }

    public async Task DeleteRoleAssignmentsAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var assignments = await (await GetQueryableAsync())
            .Where(x => x.RoleName == roleName)
            .ToListAsync(cancellationToken);

        await DeleteManyAsync(assignments, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Override to include optimized queries with proper includes.
    /// </summary>
    public override async Task<IQueryable<UserScopeAssignment>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .AsNoTracking(); // No navigation properties to include for now
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets user IDs for users who have access to a specific entity field value.
    /// Includes both direct user assignments and role-based assignments, respecting NULL=ALL semantics.
    /// </summary>
    /// <param name="fieldSelector">Function to select the field from UserScopeAssignment (e.g., x => x.DivisionId)</param>
    /// <param name="fieldValue">The specific field value to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of distinct user IDs with access to the entity</returns>
    private async Task<List<Guid>> GetUserIdsByFieldAsync(
        Func<UserScopeAssignment, Guid?> fieldSelector,
        Guid fieldValue,
        CancellationToken cancellationToken)
    {
        var dbContext = await GetDbContextAsync();

        // Get direct user assignments
        var directUserIds = await (await GetQueryableAsync())
            .Where(x =>
                x.UserId.HasValue &&
                x.IsActive &&
                !x.IsDeleted &&
                (fieldSelector(x).HasValue && fieldSelector(x).Value == fieldValue || !fieldSelector(x).HasValue))
            .Select(x => x.UserId.Value)
            .ToListAsync(cancellationToken);

        // Get role-based assignments
        var roleNames = await (await GetQueryableAsync())
            .Where(x =>
                x.RoleName != null &&
                x.IsActive &&
                !x.IsDeleted &&
                (fieldSelector(x).HasValue && fieldSelector(x).Value == fieldValue || !fieldSelector(x).HasValue))
            .Select(x => x.RoleName)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get users with those roles
        var roleBasedUserIds = new List<Guid>();
        if (roleNames.Any())
        {
            // Get role IDs for the role names
            var roles = await dbContext.Set<IdentityRole>()
                .Where(r => roleNames.Contains(r.Name))
                .ToListAsync(cancellationToken);

            var roleIds = roles.Select(r => r.Id).ToList();

            // Get users with these roles
            roleBasedUserIds = await dbContext.Set<IdentityUser>()
                .Where(u => u.Roles.Any(ur => roleIds.Contains(ur.RoleId)))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }

        // Combine and return distinct user IDs
        return directUserIds.Concat(roleBasedUserIds).Distinct().ToList();
    }

    #endregion
}
