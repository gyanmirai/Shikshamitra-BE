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

        // Get user's direct assignments
        var userAssignments = await (await GetQueryableAsync())
            .Where(x => x.UserId.HasValue && x.UserId.Value == userId && x.IsActive && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        // Get user's role names
        var user = await dbContext.Set<IdentityUser>()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return userAssignments;
        }

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        
        // Get role names from role IDs
        var userRoleNames = await dbContext.Set<IdentityRole>()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        // Get role-based assignments
        var roleAssignments = await (await GetQueryableAsync())
            .Where(x => userRoleNames.Contains(x.RoleName) && x.IsActive && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        // Combine and return
        return userAssignments.Concat(roleAssignments).ToList();
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
        return await (await GetQueryableAsync())
            .Where(x =>
                x.UserId.HasValue &&
                x.IsActive &&
                !x.IsDeleted &&
                (x.DivisionId.HasValue && x.DivisionId.Value == divisionId || !x.DivisionId.HasValue)) // NULL = all divisions
            .Select(x => x.UserId.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetUserIdsByStandardAsync(
        Guid standardId,
        CancellationToken cancellationToken = default)
    {
		var dbContext = await GetDbContextAsync();

		// Get direct user assignments
		var directUserIds = await (await GetQueryableAsync())
			.Where(x =>
				x.UserId.HasValue &&
				x.IsActive &&
				!x.IsDeleted &&
				(x.StandardId.HasValue && x.StandardId.Value == standardId || !x.StandardId.HasValue))
			.Select(x => x.UserId.Value)
			.ToListAsync(cancellationToken);

		// Get role-based assignments
		var roleNames = await (await GetQueryableAsync())
			.Where(x =>
				x.RoleName != null &&
				x.IsActive &&
				!x.IsDeleted &&
				(x.StandardId.HasValue && x.StandardId.Value == standardId || !x.StandardId.HasValue))
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
                (!x.UserId.HasValue || x.UserId.Value == userId) &&
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
}
