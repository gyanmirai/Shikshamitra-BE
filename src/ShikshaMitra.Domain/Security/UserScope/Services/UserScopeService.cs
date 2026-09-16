using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace ShikshaMitra.Security.UserScope;

/// <summary>Resolves current database authority with reuse limited to one unit of work.</summary>
public class UserScopeService(
    IUserScopeResolutionRepository repository,
    IUnitOfWorkManager unitOfWorkManager,
    ILogger<UserScopeService> logger,
    IOptions<UserScopeOptions> options) : DomainService, IUserScopeService
{
    internal const string LocalCacheKey = "ShikshaMitra.Security.Scope.UnitOfWork";

    public async Task<UserScopeContext> GetUserScopeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (CurrentTenant.Id, userId);
        var local = GetLocalCache();
        if (local?.TryGetValue(key, out var cached) == true) return cached;
        var watch = Stopwatch.StartNew();
        var roles = await repository.GetRoleNamesAsync(userId, cancellationToken);
        var identityMs = watch.Elapsed.TotalMilliseconds;
        var result = new UserScopeContext { UserId = userId, TenantId = CurrentTenant.Id };
        // Deleted, missing and foreign-tenant identities must never inherit assignments.
        if (roles != null)
        {
            result.IsAdmin = IsAdministrator(roles);
            if (!result.IsAdmin)
            {
                var hierarchy = await repository.GetReportingHierarchyAsync(cancellationToken);
                var population = ResolveReportingUsers(userId, hierarchy);
                var hierarchyMs = watch.Elapsed.TotalMilliseconds - identityMs;
                var assignments = await repository.GetAssignmentsAsync(population, cancellationToken);
                result = UserScopeContext.CreateEffective(userId, CurrentTenant.Id, assignments);
                logger.LogDebug("ScopeResolution IdentityMs={IdentityMs} ReportingMs={ReportingMs} TotalMs={TotalMs} Population={Population}",
                    identityMs, hierarchyMs, watch.Elapsed.TotalMilliseconds, population.Count);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        // No distributed cache. Failures and cancellations never become reusable authority.
        if (local != null) local[key] = result;
        return result;
    }

    public async Task<bool> IsAdminUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roles = await repository.GetRoleNamesAsync(userId, cancellationToken);
        return roles != null && IsAdministrator(roles);
    }

    private bool IsAdministrator(List<string> roles) =>
        options.Value.AdminRoles.Any(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase));

    internal static HashSet<Guid> ResolveReportingUsers(Guid userId, List<ScopeReportingEmployee> employees)
    {
        var result = new HashSet<Guid> { userId };
        var manager = employees.FirstOrDefault(employee => employee.UserId == userId);
        if (manager == null) return result;
        var reportees = employees.Where(employee => employee.ReportingManagerId.HasValue)
            .ToLookup(employee => employee.ReportingManagerId!.Value);
        var visited = new HashSet<Guid> { manager.Id };
        var pending = new Queue<Guid>();
        pending.Enqueue(manager.Id);
        while (pending.TryDequeue(out var current))
        {
            foreach (var reportee in reportees[current])
            {
                if (!visited.Add(reportee.Id)) continue;
                pending.Enqueue(reportee.Id);
                if (reportee.UserId.HasValue) result.Add(reportee.UserId.Value);
            }
        }
        return result;
    }

    private Dictionary<(Guid? TenantId, Guid UserId), UserScopeContext>? GetLocalCache()
    {
        var uow = unitOfWorkManager.Current;
        if (uow == null) return null;
        if (!uow.Items.TryGetValue(LocalCacheKey, out var value))
        {
            value = new Dictionary<(Guid?, Guid), UserScopeContext>();
            uow.Items[LocalCacheKey] = value;
        }
        return (Dictionary<(Guid?, Guid), UserScopeContext>)value;
    }

    // Public names retained. Clear all local subjects to include reporting ancestors.
    public Task InvalidateUserScopeCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ClearLocalReuse(unitOfWorkManager);
        return Task.CompletedTask;
    }

    public Task InvalidateRoleScopeCacheAsync(string roleName, CancellationToken cancellationToken = default)
    {
        ClearLocalReuse(unitOfWorkManager);
        return Task.CompletedTask;
    }

    internal static void ClearLocalReuse(IUnitOfWorkManager manager)
    {
        for (var uow = manager.Current; uow != null; uow = uow.Outer)
            uow.Items.Remove(LocalCacheKey);
    }
}

