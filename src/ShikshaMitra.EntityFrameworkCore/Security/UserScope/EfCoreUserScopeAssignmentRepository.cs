using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
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

        // Resolve the division's full hierarchy to enforce year/parent constraints for wildcard assignments.
        var divisionScope = await (
            from division in dbContext.Set<Division>()
            join standard in dbContext.Set<Standard>() on division.StandardId equals standard.Id
            join stage in dbContext.Set<EducationMediumStage>() on standard.EducationMediumStageId equals stage.Id
            join medium in dbContext.Set<EducationMedium>() on stage.EducationMediumId equals medium.Id
            join board in dbContext.Set<EducationBoard>() on medium.EducationBoardId equals board.Id
            where division.Id == divisionId
            select new
            {
                DivisionId = division.Id,
                StandardId = standard.Id,
                EducationMediumStageId = stage.Id,
                EducationMediumId = medium.Id,
                EducationBoardId = board.Id,
                AcademicYearId = board.AcademicYearId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (divisionScope == null)
        {
            return new List<Guid>();
        }

        var matchingAssignments = baseQuery
            .Where(x =>
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Exact division match
                    x.DivisionId == divisionScope.DivisionId ||
                    // Wildcard division WITHIN parent standard constraint
                    (!x.DivisionId.HasValue && x.StandardId == divisionScope.StandardId) ||
                    // Wildcard standard WITHIN parent medium stage constraint
                    (!x.StandardId.HasValue && x.EducationMediumStageId == divisionScope.EducationMediumStageId) ||
                    // Wildcard medium stage WITHIN parent medium constraint
                    (!x.EducationMediumStageId.HasValue && x.EducationMediumId == divisionScope.EducationMediumId) ||
                    // Wildcard medium WITHIN parent board constraint
                    (!x.EducationMediumId.HasValue && x.EducationBoardId == divisionScope.EducationBoardId) ||
                    // Wildcard board WITHIN academic year constraint
                    (!x.EducationBoardId.HasValue && x.AcademicYearId == divisionScope.AcademicYearId) ||
                    // Organization-wide wildcard
                    (!x.AcademicYearId.HasValue &&
                     !x.EducationBoardId.HasValue &&
                     !x.EducationMediumId.HasValue &&
                     !x.EducationMediumStageId.HasValue &&
                     !x.StandardId.HasValue &&
                     !x.DivisionId.HasValue)
                ));

        var directUserIds = await matchingAssignments
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId.Value)
            .ToListAsync(cancellationToken);

        var roleNames = await matchingAssignments
            .Where(x => x.RoleName != null)
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

        // Resolve the standard's full hierarchy to enforce year/parent constraints for wildcard assignments.
        var standardScope = await (
            from standard in dbContext.Set<Standard>()
            join stage in dbContext.Set<EducationMediumStage>() on standard.EducationMediumStageId equals stage.Id
            join medium in dbContext.Set<EducationMedium>() on stage.EducationMediumId equals medium.Id
            join board in dbContext.Set<EducationBoard>() on medium.EducationBoardId equals board.Id
            where standard.Id == standardId
            select new
            {
                StandardId = standard.Id,
                EducationMediumStageId = stage.Id,
                EducationMediumId = medium.Id,
                EducationBoardId = board.Id,
                AcademicYearId = board.AcademicYearId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (standardScope == null)
        {
            return new List<Guid>();
        }

        var matchingAssignments = baseQuery
            .Where(x =>
                x.IsActive &&
                !x.IsDeleted &&
                (
                    // Exact standard match
                    x.StandardId == standardScope.StandardId ||
                    // Wildcard standard WITHIN parent medium stage constraint
                    (!x.StandardId.HasValue && x.EducationMediumStageId == standardScope.EducationMediumStageId) ||
                    // Wildcard medium stage WITHIN parent medium constraint
                    (!x.EducationMediumStageId.HasValue && x.EducationMediumId == standardScope.EducationMediumId) ||
                    // Wildcard medium WITHIN parent board constraint
                    (!x.EducationMediumId.HasValue && x.EducationBoardId == standardScope.EducationBoardId) ||
                    // Wildcard board WITHIN academic year constraint
                    (!x.EducationBoardId.HasValue && x.AcademicYearId == standardScope.AcademicYearId) ||
                    // Organization-wide wildcard
                    (!x.AcademicYearId.HasValue &&
                     !x.EducationBoardId.HasValue &&
                     !x.EducationMediumId.HasValue &&
                     !x.EducationMediumStageId.HasValue &&
                     !x.StandardId.HasValue &&
                     !x.DivisionId.HasValue)
                ));

        var directUserIds = await matchingAssignments
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId.Value)
            .ToListAsync(cancellationToken);

        var roleNames = await matchingAssignments
            .Where(x => x.RoleName != null)
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
