using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        var dbContext = await GetDbContextAsync();
        var baseQuery = await GetQueryableAsync();

        // Get the division's standard ID to enforce hierarchical constraints
        var division = await dbContext.Set<ShikshaMitra.TenantSettings.Divisions.Division>()
            .Where(d => d.Id == divisionId)
            .Select(d => new { d.StandardId })
            .FirstOrDefaultAsync(cancellationToken);

        if (division == null)
        {
            return new List<Guid>();
        }

        // Get direct user assignments with hierarchical validation
        // User has access if:
        // 1. Exact division match (DivisionId == divisionId), OR
        // 2. Wildcard division (DivisionId == null) WITHIN the division's parent standard
        var directUserIds = await baseQuery
            .Where(x =>
                x.UserId.HasValue &&
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Exact division match
                    (x.DivisionId.HasValue && x.DivisionId.Value == divisionId) ||
                    // Wildcard division WITHIN parent standard constraint
                    (!x.DivisionId.HasValue && x.StandardId.HasValue && x.StandardId.Value == division.StandardId) ||
                    // Wildcard standard (implies access to all divisions in that standard)
                    (!x.StandardId.HasValue && !x.DivisionId.HasValue)
                ))
            .Select(x => x.UserId.Value)
            .ToListAsync(cancellationToken);

        // Get role-based assignments with hierarchical validation
        var roleNames = await baseQuery
            .Where(x =>
                x.RoleName != null &&
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Exact division match
                    (x.DivisionId.HasValue && x.DivisionId.Value == divisionId) ||
                    // Wildcard division WITHIN parent standard constraint
                    (!x.DivisionId.HasValue && x.StandardId.HasValue && x.StandardId.Value == division.StandardId) ||
                    // Wildcard standard (implies access to all divisions in that standard)
                    (!x.StandardId.HasValue && !x.DivisionId.HasValue)
                ))
            .Select(x => x.RoleName)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get users with those roles
        var roleBasedUserIds = new List<Guid>();
        if (roleNames.Any())
        {
            roleBasedUserIds = await (
                from user in dbContext.Set<IdentityUser>()
                from userRole in user.Roles
                join role in dbContext.Set<IdentityRole>() on userRole.RoleId equals role.Id
                where roleNames.Contains(role.Name)
                select user.Id
            ).Distinct().ToListAsync(cancellationToken);
        }

        // Combine and return distinct user IDs
        return directUserIds.Concat(roleBasedUserIds).Distinct().ToList();
    }

    public async Task<List<Guid>> GetUserIdsByStandardAsync(
        Guid standardId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var baseQuery = await GetQueryableAsync();

        // Get the standard's parent ID (EducationMediumStageId) to enforce hierarchical constraints
        var standard = await dbContext.Set<ShikshaMitra.TenantSettings.Standards.Standard>()
            .Where(s => s.Id == standardId)
            .Select(s => new { s.EducationMediumStageId })
            .FirstOrDefaultAsync(cancellationToken);

        if (standard == null)
        {
            return new List<Guid>();
        }

        // Get direct user assignments with hierarchical validation
        // User has access if:
        // 1. Exact standard match (StandardId == standardId), OR
        // 2. Wildcard standard (StandardId == null) WITHIN the standard's parent medium stage, OR
        // 3. Wildcard medium stage (implies access to all standards in that stage)
        var directUserIds = await baseQuery
            .Where(x =>
                x.UserId.HasValue &&
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Exact standard match
                    (x.StandardId.HasValue && x.StandardId.Value == standardId) ||
                    // Wildcard standard WITHIN parent medium stage constraint
                    (!x.StandardId.HasValue && x.EducationMediumStageId.HasValue && 
                     x.EducationMediumStageId.Value == standard.EducationMediumStageId) ||
                    // Wildcard medium stage (implies access to all standards in any stage)
                    (!x.EducationMediumStageId.HasValue && !x.StandardId.HasValue)
                ))
            .Select(x => x.UserId.Value)
            .ToListAsync(cancellationToken);

        // Get role-based assignments with hierarchical validation
        var roleNames = await baseQuery
            .Where(x =>
                x.RoleName != null &&
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Exact standard match
                    (x.StandardId.HasValue && x.StandardId.Value == standardId) ||
                    // Wildcard standard WITHIN parent medium stage constraint
                    (!x.StandardId.HasValue && x.EducationMediumStageId.HasValue && 
                     x.EducationMediumStageId.Value == standard.EducationMediumStageId) ||
                    // Wildcard medium stage (implies access to all standards in any stage)
                    (!x.EducationMediumStageId.HasValue && !x.StandardId.HasValue)
                ))
            .Select(x => x.RoleName)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get users with those roles
        var roleBasedUserIds = new List<Guid>();
        if (roleNames.Any())
        {
            roleBasedUserIds = await (
                from user in dbContext.Set<IdentityUser>()
                from userRole in user.Roles
                join role in dbContext.Set<IdentityRole>() on userRole.RoleId equals role.Id
                where roleNames.Contains(role.Name)
                select user.Id
            ).Distinct().ToListAsync(cancellationToken);
        }

        // Combine and return distinct user IDs
        return directUserIds.Concat(roleBasedUserIds).Distinct().ToList();
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
    /// <param name="fieldPredicate">Expression predicate to filter by field (e.g., x => x.DivisionId == divisionId || x.DivisionId == null)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of distinct user IDs with access to the entity</returns>
    private async Task<List<Guid>> GetUserIdsByFieldAsync(
        Expression<Func<UserScopeAssignment, bool>> fieldPredicate,
        CancellationToken cancellationToken)
    {
        var dbContext = await GetDbContextAsync();
        var baseQuery = await GetQueryableAsync();

        // Get direct user assignments
        var directUserIds = await baseQuery
            .Where(x => x.UserId.HasValue && x.IsActive && !x.IsDeleted)
            .Where(fieldPredicate)
            .Select(x => x.UserId.Value)
            .ToListAsync(cancellationToken);

        // Get role-based assignments
        var roleNames = await baseQuery
            .Where(x => x.RoleName != null && x.IsActive && !x.IsDeleted)
            .Where(fieldPredicate)
            .Select(x => x.RoleName)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get users with those roles
        var roleBasedUserIds = new List<Guid>();
        if (roleNames.Any())
        {
            // Get user IDs with those roles using a single join query
            roleBasedUserIds = await (
                from user in dbContext.Set<IdentityUser>()
                from userRole in user.Roles
                join role in dbContext.Set<IdentityRole>() on userRole.RoleId equals role.Id
                where roleNames.Contains(role.Name)
                select user.Id
            ).Distinct().ToListAsync(cancellationToken);
        }

        // Combine and return distinct user IDs
        return directUserIds.Concat(roleBasedUserIds).Distinct().ToList();
    }

    #endregion
}
