using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly ILogger<UserScopeService> _logger;
    private readonly UserScopeOptions _options;

    public UserScopeService(
        IUserScopeAssignmentRepository scopeRepository,
        IDistributedCache<UserScopeContext> cache,
        IIdentityUserRepository userRepository,
        IIdentityUserRepositoryExtensions userRepositoryExtensions,
        IIdentityRoleRepository roleRepository,
        ICurrentUser currentUser,
        ILogger<UserScopeService> logger,
        IOptions<UserScopeOptions> options)
    {
        _scopeRepository = scopeRepository;
        _cache = cache;
        _userRepository = userRepository;
        _userRepositoryExtensions = userRepositoryExtensions;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
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

        // Get user's scope assignments
        var assignments = await _scopeRepository.GetUserAssignmentsAsync(userId, cancellationToken);

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
        var user = await _userRepository.GetAsync(userId, cancellationToken: cancellationToken);
        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        
        // Get role names from role repository
        var roles = await _roleRepository.GetListAsync(cancellationToken: cancellationToken);
        var userRoleNames = roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToList();

        return _options.AdminRoles.Any(adminRole =>
            userRoleNames.Contains(adminRole, StringComparer.OrdinalIgnoreCase));
    }

    #region Private Methods

    private UserScopeContext BuildScopeFromAssignments(Guid userId, List<UserScopeAssignment> assignments)
    {
        var scope = new UserScopeContext
        {
            UserId = userId,
            TenantId = CurrentTenant.Id,
            IsAdmin = false,
            CalculatedAt = DateTime.UtcNow,
            FromCache = false
        };

        foreach (var assignment in assignments.Where(a => a.IsActive))
        {
            // Implement NULL = ALL principle:
            // - If a field is NULL, user has access to ALL entities at that level
            // - If a field has a value, user has access to that specific entity and its children

            // Academic Year: NULL = ALL years
            if (assignment.AcademicYearId.HasValue)
                scope.AcademicYearIds.Add(assignment.AcademicYearId.Value);
            else
                scope.HasWildcardAcademicYear = true;

            // Education Board: NULL = ALL boards
            if (assignment.EducationBoardId.HasValue)
                scope.BoardIds.Add(assignment.EducationBoardId.Value);
            else
                scope.HasWildcardBoard = true;

            // Education Medium: NULL = ALL mediums (within assigned board or all if board is also wildcard)
            if (assignment.EducationMediumId.HasValue)
                scope.MediumIds.Add(assignment.EducationMediumId.Value);
            else
                scope.HasWildcardMedium = true;

            // Education Medium Stage: NULL = ALL stages (within assigned medium/board hierarchy)
            if (assignment.EducationMediumStageId.HasValue)
                scope.MediumStageIds.Add(assignment.EducationMediumStageId.Value);
            else
                scope.HasWildcardMediumStage = true;

            // Standard/Class: NULL = ALL standards (within assigned stage/medium/board hierarchy)
            if (assignment.StandardId.HasValue)
                scope.StandardIds.Add(assignment.StandardId.Value);
            else
                scope.HasWildcardStandard = true;

            // Division/Section: NULL = ALL divisions (within assigned standard hierarchy)
            if (assignment.DivisionId.HasValue)
                scope.DivisionIds.Add(assignment.DivisionId.Value);
            else
                scope.HasWildcardDivision = true;
        }

        // Remove duplicates
        scope.AcademicYearIds = scope.AcademicYearIds.Distinct().ToList();
        scope.BoardIds = scope.BoardIds.Distinct().ToList();
        scope.MediumIds = scope.MediumIds.Distinct().ToList();
        scope.MediumStageIds = scope.MediumStageIds.Distinct().ToList();
        scope.StandardIds = scope.StandardIds.Distinct().ToList();
        scope.DivisionIds = scope.DivisionIds.Distinct().ToList();

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
        return $"UserScope:{CurrentTenant.Id}:{userId}";
    }

    #endregion
}

