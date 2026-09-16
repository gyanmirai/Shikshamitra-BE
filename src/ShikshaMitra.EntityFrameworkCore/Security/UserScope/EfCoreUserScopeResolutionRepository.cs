using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Security.UserScope;

public sealed class EfCoreUserScopeResolutionRepository(
    IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
    : IUserScopeResolutionRepository, ITransientDependency
{
    public async Task<List<string>?> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db = await contexts.GetDbContextAsync();
        var users = db.Set<IdentityUser>().IgnoreAutoIncludes().AsNoTracking()
            .Where(user => user.TenantId == tenant.Id && user.Id == userId);
        if (!await users.AnyAsync(cancellationToken)) return null;
        return await (from user in users
                      from membership in user.Roles
                      join role in db.Set<IdentityRole>() on membership.RoleId equals role.Id
                      where role.TenantId == tenant.Id
                      select role.Name).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<List<ScopeReportingEmployee>> GetReportingHierarchyAsync(CancellationToken cancellationToken = default)
    {
        var db = await contexts.GetDbContextAsync();
        return await db.Set<Employee>().IgnoreAutoIncludes().AsNoTracking()
            .Where(employee => employee.TenantId == tenant.Id)
            .Select(employee => new ScopeReportingEmployee(employee.Id, employee.UserId, employee.ReportingManagerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserScopeAssignment>> GetAssignmentsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return [];
        var db = await contexts.GetDbContextAsync();
        var users = db.Set<IdentityUser>().IgnoreAutoIncludes().AsNoTracking()
            .Where(user => user.TenantId == tenant.Id && userIds.Contains(user.Id));
        var roles = from user in users
                    from membership in user.Roles
                    join role in db.Set<IdentityRole>() on membership.RoleId equals role.Id
                    where role.TenantId == tenant.Id
                    select role.Name;
        var validUsers = users.Select(user => user.Id);
        return await db.Set<UserScopeAssignment>().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenant.Id && assignment.IsActive && !assignment.IsDeleted &&
                ((assignment.UserId.HasValue && validUsers.Contains(assignment.UserId.Value)) ||
                 (assignment.RoleName != null && roles.Contains(assignment.RoleName))))
            .ToListAsync(cancellationToken);
    }
}
