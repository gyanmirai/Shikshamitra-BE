using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Domain service implementation for user scope operations.
/// </summary>
public class UserScopeService : DomainService, IUserScopeService
{
    private readonly IUserScopeAssignmentRepository _scopeRepository;
    private readonly IDistributedCache<UserScopeContext> _cache;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IIdentityUserRepositoryExtensions _userRepositoryExtensions;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<UserScopeService> _logger;
    private readonly UserScopeOptions _options;

    public UserScopeService(
        IUserScopeAssignmentRepository scopeRepository,
        IDistributedCache<UserScopeContext> cache,
        IIdentityUserRepository userRepository,
        IIdentityUserRepositoryExtensions userRepositoryExtensions,
        IIdentityRoleRepository roleRepository,
        ICurrentUser currentUser,
        IEmployeeRepository employeeRepository,
        ILogger<UserScopeService> logger,
        IOptions<UserScopeOptions> options)
    {
        _scopeRepository = scopeRepository;
        _cache = cache;
        _userRepository = userRepository;
        _userRepositoryExtensions = userRepositoryExtensions;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
        _employeeRepository = employeeRepository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<UserScopeContext> GetUserScopeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_options.CacheEnabled)
        {
            var cacheKey = GetCacheKey(userId);
            var cachedScope = await _cache.GetAsync(cacheKey, token: cancellationToken);

            if (cachedScope != null)
            {
                _logger.LogDebug("UserScope cache hit for user {UserId}", userId);
                cachedScope.FromCache = true;
                return cachedScope;
            }
        }

        // Check if user is admin
        var isAdmin = await IsAdminUserAsync(userId, cancellationToken);
        if (isAdmin)
        {
            var adminScope = CreateAdminScope(userId);
            await CacheScopeAsync(userId, adminScope, cancellationToken);
            return adminScope;
        }

        // Get the user's direct scope assignments and inherit the scopes of all
        // direct and indirect reportees. The reporting hierarchy grants only the
        // same data visibility those employees already possess; it never creates
        // any independent scope of its own.
        var assignments = await _scopeRepository.GetUserAssignmentsAsync(userId, cancellationToken);
        var reporteeUserIds = await GetReporteeUserIdsAsync(userId, cancellationToken);
        foreach (var reporteeUserId in reporteeUserIds)
        {
            assignments.AddRange(await _scopeRepository.GetUserAssignmentsAsync(reporteeUserId, cancellationToken));
        }

        if (!assignments.Any())
        {
            _logger.LogWarning("No scope assignments found for user {UserId}", userId);
            return CreateEmptyScope(userId);
        }

        // Build scope context from assignments
        var scope = BuildScopeFromAssignments(userId, assignments);

        // Cache the result
        await CacheScopeAsync(userId, scope, cancellationToken);

        return scope;
    }

    public async Task InvalidateUserScopeCacheAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (_options.CacheEnabled)
        {
            var cacheKey = GetCacheKey(userId);
            await _cache.RemoveAsync(cacheKey, token: cancellationToken);
            _logger.LogInformation("Invalidated scope cache for user {UserId}", userId);
        }
    }

    public async Task InvalidateRoleScopeCacheAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CacheEnabled || string.IsNullOrWhiteSpace(roleName))
        {
            return;
        }

        _logger.LogInformation("Invalidating scope cache for all users with role {RoleName}", roleName);

        // Get the role by name
        var role = await _roleRepository.FindByNormalizedNameAsync(
            roleName.ToUpperInvariant(),
            cancellationToken: cancellationToken);

        if (role == null)
        {
            _logger.LogWarning("Role {RoleName} not found, skipping cache invalidation", roleName);
            return;
        }

        // Efficiently get user IDs with this role at the database level
        var userIdsToInvalidate = await _userRepositoryExtensions.GetUserIdsByRoleIdAsync(
            role.Id,
            cancellationToken);

        _logger.LogInformation(
            "Found {Count} users with role {RoleName}, invalidating their caches",
            userIdsToInvalidate.Count, roleName);

        // Invalidate cache for each user
        foreach (var userId in userIdsToInvalidate)
        {
            await InvalidateUserScopeCacheAsync(userId, cancellationToken);
        }

        _logger.LogInformation(
            "Completed cache invalidation for {Count} users with role {RoleName}",
            userIdsToInvalidate.Count, roleName);
    }

    public async Task<bool> IsAdminUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FindAsync(userId, includeDetails: true, cancellationToken: cancellationToken);
        if (user is null)
        {
            return false;
        }

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();

        if (!roleIds.Any())
        {
            return false;
        }

        // Get role names from role repository - only fetch roles matching the user's role IDs
        var allRoles = await _roleRepository.GetListAsync(cancellationToken: cancellationToken);
        var userRoleNames = allRoles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToList();

        return _options.AdminRoles.Any(adminRole =>
            userRoleNames.Contains(adminRole, StringComparer.OrdinalIgnoreCase));
    }

    #region Private Methods

    private UserScopeContext BuildScopeFromAssignments(Guid userId, List<UserScopeAssignment> assignments)
    {
        var scope = UserScopeContext.CreateEffective(userId, CurrentTenant.Id, assignments);

        _logger.LogDebug(
            "Built scope for user {UserId}: Wildcards=[AY:{WildcardYear},B:{WildcardBoard},M:{WildcardMedium},MS:{WildcardStage},S:{WildcardStd},D:{WildcardDiv}], IDs=[Boards:{Boards},Standards:{Standards},Divisions:{Divisions}]",
            userId,
            scope.HasWildcardAcademicYear, scope.HasWildcardBoard, scope.HasWildcardMedium,
            scope.HasWildcardMediumStage, scope.HasWildcardStandard, scope.HasWildcardDivision,
            scope.BoardIds.Count, scope.StandardIds.Count, scope.DivisionIds.Count);

        return scope;
    }

    private UserScopeContext CreateAdminScope(Guid userId)
    {
        return new UserScopeContext
        {
            UserId = userId,
            TenantId = CurrentTenant.Id,
            IsAdmin = true,
            CalculatedAt = DateTime.UtcNow,
            FromCache = false
        };
    }

    private UserScopeContext CreateEmptyScope(Guid userId)
    {
        return new UserScopeContext
        {
            UserId = userId,
            TenantId = CurrentTenant.Id,
            IsAdmin = false,
            CalculatedAt = DateTime.UtcNow,
            FromCache = false
        };
    }

    private async Task CacheScopeAsync(
        Guid userId,
        UserScopeContext scope,
        CancellationToken cancellationToken)
    {
        if (_options.CacheEnabled)
        {
            var cacheKey = GetCacheKey(userId);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(_options.CacheDurationMinutes)
            };

            await _cache.SetAsync(cacheKey, scope, cacheOptions, token: cancellationToken);
            _logger.LogDebug("Cached scope for user {UserId} with TTL {TTL}min",
                userId, _options.CacheDurationMinutes);
        }
    }

    private string GetCacheKey(Guid userId)
    {
        return $"UserScope:v2:{CurrentTenant.Id}:{userId}";
    }

    private async Task<List<Guid>> GetReporteeUserIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var manager = await _employeeRepository.GetEmployeeByUserIdAsync(userId, cancellationToken);
        if (manager == null)
        {
            return [];
        }

        var employees = await _employeeRepository.GetListAsync(cancellationToken: cancellationToken);
        var reporteesByManager = employees
            .Where(employee => employee.ReportingManagerId.HasValue)
            .GroupBy(employee => employee.ReportingManagerId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var visited = new HashSet<Guid> { manager.Id };
        var pendingManagers = new Queue<Guid>();
        var reporteeUserIds = new List<Guid>();
        pendingManagers.Enqueue(manager.Id);

        while (pendingManagers.Count > 0)
        {
            var managerId = pendingManagers.Dequeue();
            if (!reporteesByManager.TryGetValue(managerId, out var directReportees))
            {
                continue;
            }

            foreach (var reportee in directReportees)
            {
                if (!visited.Add(reportee.Id))
                {
                    continue;
                }

                pendingManagers.Enqueue(reportee.Id);
                if (reportee.UserId.HasValue)
                {
                    reporteeUserIds.Add(reportee.UserId.Value);
                }
            }
        }

        return reporteeUserIds;
    }

    #endregion
}

