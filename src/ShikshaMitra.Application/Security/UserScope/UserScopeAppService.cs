using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Security.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Application service implementation for user scope management.
/// </summary>
[Authorize]
public class UserScopeAppService : ApplicationService, IUserScopeAppService
{
    private readonly IUserScopeAssignmentRepository _scopeRepository;
    private readonly IUserScopeService _userScopeService;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IRepository<UserScopeAssignment, Guid> _repository;
    private readonly ILogger<UserScopeAppService> _logger;

    public UserScopeAppService(
        IUserScopeAssignmentRepository scopeRepository,
        IUserScopeService userScopeService,
        IIdentityUserRepository userRepository,
        IRepository<UserScopeAssignment, Guid> repository,
        ILogger<UserScopeAppService> logger)
    {
        _scopeRepository = scopeRepository;
        _userScopeService = userScopeService;
        _userRepository = userRepository;
        _repository = repository;
        _logger = logger;
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Assign)]
    public async Task<UserScopeAssignmentDto> AssignScopeAsync(CreateUserScopeAssignmentDto input)
    {
        try
        {
            _logger.LogInformation("Attempting to assign scope. UserId: {UserId}, RoleName: {RoleName}", 
                input.UserId, input.RoleName);

            // Note: This validation is also enforced at DTO level via IValidatableObject
            // Keeping it here as a defensive programming practice
            // Validation: Either UserId OR RoleName must be provided
            if ((!input.UserId.HasValue && string.IsNullOrWhiteSpace(input.RoleName)) ||
                (input.UserId.HasValue && !string.IsNullOrWhiteSpace(input.RoleName)))
            {
                throw new UserFriendlyException(
                    "Either UserId or RoleName must be provided, but not both.",
                    code: "UserScope:InvalidInput"
                );
            }

            // Validate user exists if UserId is provided
            if (input.UserId.HasValue)
            {
                var userExists = await _userRepository.FindAsync(input.UserId.Value);
                if (userExists == null)
                {
                    throw new UserFriendlyException(
                        $"User with ID '{input.UserId.Value}' does not exist.",
                        code: "UserScope:UserNotFound"
                    );
                }
            }

            // Create new assignment
            var assignment = new UserScopeAssignment(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                input.UserId,
                input.RoleName,
                input.AcademicYearId,
                input.EducationBoardId,
                input.EducationMediumId,
                input.EducationMediumStageId,
                input.StandardId,
                input.DivisionId,
                input.Description
            );

            try
            {
                await _scopeRepository.InsertAsync(assignment);
            }
            catch (Exception dbEx) when (IsConstraintViolationException(dbEx))
            {
                // Handle unique constraint violation (UQ_UserScopeAssignment)
                if (IsUniqueConstraintViolation(dbEx))
                {
                    _logger.LogWarning(dbEx, 
                        "Duplicate scope assignment detected. UserId: {UserId}, RoleName: {RoleName}",
                        input.UserId, input.RoleName);
                    throw new UserFriendlyException(
                        "This scope assignment already exists for the user/role with the specified parameters.",
                        code: "UserScope:AlreadyExists"
                    );
                }

                // Handle foreign key constraint violation
                if (IsForeignKeyViolation(dbEx))
                {
                    var correlationId = Guid.NewGuid();
                    _logger.LogWarning(dbEx, 
                        "Foreign key violation during scope assignment. UserId: {UserId}, RoleName: {RoleName}, CorrelationId: {CorrelationId}",
                        input.UserId, input.RoleName, correlationId);
                    throw new UserFriendlyException(
                        $"One or more referenced entities (Standard, Division, Board, Medium, etc.) do not exist. Please verify all IDs are valid. (Ref: {correlationId})",
                        code: "UserScope:ForeignKeyViolation"
                    );
                }

                throw;
            }

            // Invalidate cache for the user or role
            if (input.UserId.HasValue)
            {
                await _userScopeService.InvalidateUserScopeCacheAsync(input.UserId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(input.RoleName))
            {
                // Invalidate cache for all users with this role
                await _userScopeService.InvalidateRoleScopeCacheAsync(input.RoleName);
            }

            _logger.LogInformation(
                "User scope assigned successfully. UserId: {UserId}, RoleName: {RoleName}, AssignmentId: {AssignmentId}",
                input.UserId, input.RoleName, assignment.Id
            );

            return ObjectMapper.Map<UserScopeAssignment, UserScopeAssignmentDto>(assignment);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Error assigning user scope. UserId: {UserId}, RoleName: {RoleName}, CorrelationId: {CorrelationId}",
                input.UserId, input.RoleName, correlationId);

            throw new UserFriendlyException(
                $"An error occurred while assigning the scope. Please contact support with reference: {correlationId}",
                code: "UserScope:AssignmentFailed"
            );
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.BulkAssign)]
    public async Task<int> BulkAssignAsync(BulkScopeAssignmentDto input)
    {
        var correlationId = Guid.NewGuid();
        var assignmentsCreated = 0;
        var successfulUserIds = new List<Guid>();

        try
        {
            _logger.LogInformation(
                "Attempting bulk scope assignment. UserCount: {UserCount}, CorrelationId: {CorrelationId}",
                input.UserIds?.Count ?? 0, correlationId);

            // Validate input
            if (input.UserIds == null || !input.UserIds.Any())
            {
                throw new BusinessException(
                    code: "UserScope:NoUsers",
                    message: "At least one user ID must be provided."
                );
            }

            // Validate all users exist before processing
            var distinctUserIds = input.UserIds.Distinct().ToList();
            var missingUserIds = new List<Guid>();

            foreach (var userId in distinctUserIds)
            {
                var userExists = await _userRepository.FindAsync(userId);
                if (userExists == null)
                {
                    missingUserIds.Add(userId);
                }
            }

            if (missingUserIds.Any())
            {
                _logger.LogWarning(
                    "Bulk assignment failed - users not found. MissingUserIds: [{MissingUserIds}], CorrelationId: {CorrelationId}",
                    string.Join(", ", missingUserIds), correlationId);

                throw new BusinessException(
                    code: "UserScope:UsersNotFound",
                    message: $"The following user IDs do not exist: {string.Join(", ", missingUserIds)}. (Ref: {correlationId})"
                );
            }

            var failedAssignments = new List<(Guid UserId, Guid? DivisionId, string Error)>();

            foreach (var userId in distinctUserIds)
            {
                var userAssignmentCount = 0;

                try
                {
                    // If specific divisions are provided, create one assignment per division
                    if (input.DivisionIds != null && input.DivisionIds.Any())
                    {
                        foreach (var divisionId in input.DivisionIds)
                        {
                            try
                            {
                                var assignment = new UserScopeAssignment(
                                    GuidGenerator.Create(),
                                    CurrentTenant.Id,
                                    userId,
                                    null,
                                    input.AcademicYearId,
                                    input.EducationBoardId,
                                    input.EducationMediumId,
                                    input.EducationMediumStageId,
                                    input.StandardId,
                                    divisionId,
                                    input.Description
                                );

                                await _scopeRepository.InsertAsync(assignment);
                                assignmentsCreated++;
                                userAssignmentCount++;
                            }
                            catch (Exception dbEx) when (IsConstraintViolationException(dbEx))
                            {
                                if (IsUniqueConstraintViolation(dbEx))
                                {
                                    _logger.LogDebug(
                                        "Skipping duplicate assignment for UserId: {UserId}, DivisionId: {DivisionId}, CorrelationId: {CorrelationId}",
                                        userId, divisionId, correlationId);
                                    continue;
                                }

                                if (IsForeignKeyViolation(dbEx))
                                {
                                    failedAssignments.Add((userId, divisionId, "Invalid reference data"));
                                    _logger.LogWarning(dbEx,
                                        "Foreign key violation in bulk assign for UserId: {UserId}, DivisionId: {DivisionId}, CorrelationId: {CorrelationId}",
                                        userId, divisionId, correlationId);
                                    continue;
                                }

                                throw;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            var assignment = new UserScopeAssignment(
                                GuidGenerator.Create(),
                                CurrentTenant.Id,
                                userId,
                                null,
                                input.AcademicYearId,
                                input.EducationBoardId,
                                input.EducationMediumId,
                                input.EducationMediumStageId,
                                input.StandardId,
                                null,
                                input.Description
                            );

                            await _scopeRepository.InsertAsync(assignment);
                            assignmentsCreated++;
                            userAssignmentCount++;
                        }
                        catch (Exception dbEx) when (IsConstraintViolationException(dbEx))
                        {
                            if (IsUniqueConstraintViolation(dbEx))
                            {
                                _logger.LogDebug(
                                    "Skipping duplicate assignment for UserId: {UserId}, CorrelationId: {CorrelationId}",
                                    userId, correlationId);
                                continue;
                            }

                            if (IsForeignKeyViolation(dbEx))
                            {
                                failedAssignments.Add((userId, null, "Invalid reference data"));
                                _logger.LogWarning(dbEx,
                                    "Foreign key violation in bulk assign for UserId: {UserId}, CorrelationId: {CorrelationId}",
                                    userId, correlationId);
                                continue;
                            }

                            throw;
                        }
                    }

                    // Track users with successful assignments for cache invalidation
                    if (userAssignmentCount > 0)
                    {
                        successfulUserIds.Add(userId);
                    }
                }
                catch (UserFriendlyException)
                {
                    throw;
                }
                catch (BusinessException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Unexpected error processing bulk assignment for UserId: {UserId}, CorrelationId: {CorrelationId}",
                        userId, correlationId);
                    throw;
                }
            }

            // Invalidate cache for all users with successful assignments
            foreach (var userId in successfulUserIds)
            {
                await _userScopeService.InvalidateUserScopeCacheAsync(userId);
            }

            if (failedAssignments.Any())
            {
                _logger.LogWarning(
                    "Bulk assign completed with {FailedCount} failures. Assignments created: {Created}, CorrelationId: {CorrelationId}",
                    failedAssignments.Count, assignmentsCreated, correlationId);
            }
            else
            {
                _logger.LogInformation(
                    "Bulk scope assignment completed successfully. Assignments created: {Created}, Users processed: {UserCount}, CorrelationId: {CorrelationId}",
                    assignmentsCreated, successfulUserIds.Count, correlationId);
            }

            return assignmentsCreated;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during bulk scope assignment. Assignments created: {Created}, CorrelationId: {CorrelationId}",
                assignmentsCreated, correlationId);

            throw new UserFriendlyException(
                $"Bulk assignment partially completed ({assignmentsCreated} assignments created) before encountering an error. Please contact support with reference: {correlationId}",
                code: "UserScope:BulkAssignFailed"
            );
        }
        finally
        {
            // Ensure cache invalidation happens even if there's an error after some successful assignments
            if (successfulUserIds.Any())
            {
                try
                {
                    foreach (var userId in successfulUserIds)
                    {
                        await _userScopeService.InvalidateUserScopeCacheAsync(userId);
                    }
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx,
                        "Failed to invalidate cache for some users during bulk assignment. CorrelationId: {CorrelationId}",
                        correlationId);
                }
            }
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Revoke)]
    public async Task RevokeScopeAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Attempting to revoke scope assignment {AssignmentId}", id);

            var assignment = await _repository.FindAsync(id);
            if (assignment == null)
            {
                throw new UserFriendlyException(
                    $"Scope assignment with ID '{id}' does not exist.",
                    code: "UserScope:AssignmentNotFound"
                );
            }

            await _repository.DeleteAsync(assignment);

            // Invalidate cache for user or role
            if (assignment.UserId.HasValue)
            {
                await _userScopeService.InvalidateUserScopeCacheAsync(assignment.UserId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(assignment.RoleName))
            {
                // Invalidate cache for all users with this role
                await _userScopeService.InvalidateRoleScopeCacheAsync(assignment.RoleName);
            }

            _logger.LogInformation("Scope assignment {AssignmentId} revoked successfully", id);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Error revoking scope assignment {AssignmentId}, CorrelationId: {CorrelationId}", id, correlationId);
            throw new UserFriendlyException(
                $"An error occurred while revoking the scope assignment. Please contact support with reference: {correlationId}",
                code: "UserScope:RevokeFailed"
            );
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Revoke)]
    public async Task RevokeUserScopesAsync(Guid userId)
    {
        await _scopeRepository.DeleteUserAssignmentsAsync(userId);
        await _userScopeService.InvalidateUserScopeCacheAsync(userId);
    }

    [Authorize(ShikshaMitraPermissions.UserScope.View)]
    public async Task<UserScopeDto> GetUserScopeAsync(Guid userId)
    {
        try
        {
            _logger.LogDebug("Getting scope information for user {UserId}", userId);

            // Get user info
            var user = await _userRepository.FindAsync(userId);
            if (user == null)
            {
                throw new UserFriendlyException(
                    $"User with ID '{userId}' does not exist.",
                    code: "UserScope:UserNotFound"
                );
            }

            // Get user's scope context
            var scopeContext = await _userScopeService.GetUserScopeAsync(userId);

            // Get all assignments
            var assignments = await _scopeRepository.GetUserAssignmentsAsync(userId);

            var result = new UserScopeDto
            {
                UserId = userId,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                IsAdmin = scopeContext.IsAdmin,
                Assignments = ObjectMapper.Map<List<UserScopeAssignment>, List<UserScopeAssignmentDto>>(assignments),
                EffectiveScope = ObjectMapper.Map<UserScopeContext, UserScopeContextDto>(scopeContext),
                FromCache = scopeContext.FromCache
            };

            return result;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Error getting scope for user {UserId}, CorrelationId: {CorrelationId}", userId, correlationId);
            throw new UserFriendlyException(
                $"An error occurred while retrieving user scope. Please contact support with reference: {correlationId}",
                code: "UserScope:GetScopeFailed"
            );
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.View)]
    public async Task<List<Guid>> GetUsersByDivisionAsync(Guid divisionId)
    {
        return await _scopeRepository.GetUserIdsByDivisionAsync(divisionId);
    }

    [Authorize(ShikshaMitraPermissions.UserScope.View)]
    public async Task<List<Guid>> GetUsersByStandardAsync(Guid standardId)
    {
        return await _scopeRepository.GetUserIdsByStandardAsync(standardId);
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Manage)]
    public async Task<List<UserScopeAssignmentDto>> GetAllAssignmentsAsync(Guid? userId = null, string? roleName = null)
    {
        var query = await _repository.GetQueryableAsync();

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            query = query.Where(x => x.RoleName == roleName);
        }

        var assignments = await AsyncExecuter.ToListAsync(query);
        return ObjectMapper.Map<List<UserScopeAssignment>, List<UserScopeAssignmentDto>>(assignments);
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Manage)]
    public async Task ActivateScopeAsync(Guid id)
    {
        var assignment = await _repository.GetAsync(id);
        assignment.Activate();
        await _repository.UpdateAsync(assignment);

        // Invalidate cache for user or role
        if (assignment.UserId.HasValue)
        {
            await _userScopeService.InvalidateUserScopeCacheAsync(assignment.UserId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(assignment.RoleName))
        {
            await _userScopeService.InvalidateRoleScopeCacheAsync(assignment.RoleName);
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Manage)]
    public async Task DeactivateScopeAsync(Guid id)
    {
        var assignment = await _repository.GetAsync(id);
        assignment.Deactivate();
        await _repository.UpdateAsync(assignment);

        // Invalidate cache for user or role
        if (assignment.UserId.HasValue)
        {
            await _userScopeService.InvalidateUserScopeCacheAsync(assignment.UserId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(assignment.RoleName))
        {
            await _userScopeService.InvalidateRoleScopeCacheAsync(assignment.RoleName);
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Manage)]
    public async Task UpdateDescriptionAsync(Guid id, string description)
    {
        var assignment = await _repository.GetAsync(id);
        assignment.UpdateDescription(description);
        await _repository.UpdateAsync(assignment);
    }

    /// <summary>
    /// Checks if an exception is likely a database constraint violation.
    /// </summary>
    private static bool IsConstraintViolationException(Exception ex)
    {
        // Check exception type name (works across different providers)
        var exceptionTypeName = ex.GetType().Name;
        if (exceptionTypeName.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase) ||
            exceptionTypeName.Contains("Constraint", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check inner exception type
        if (ex.InnerException != null)
        {
            var innerTypeName = ex.InnerException.GetType().Name;
            if (innerTypeName.Contains("Postgres", StringComparison.OrdinalIgnoreCase) ||
                innerTypeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase) ||
                innerTypeName.Contains("Database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an exception is caused by a unique constraint violation.
    /// Uses multiple detection strategies for cross-database compatibility.
    /// </summary>
    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        // Check exception message and inner exception message
        var message = ex.Message;
        var innerMessage = ex.InnerException?.Message ?? string.Empty;
        var fullMessage = $"{message} {innerMessage}";

        // Check for constraint name
        if (fullMessage.Contains("UQ_UserScopeAssignment", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for unique constraint keywords
        if (fullMessage.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("unique_violation", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("23505", StringComparison.Ordinal)) // PostgreSQL error code
        {
            return true;
        }

        // Check inner exception properties using reflection for Npgsql/SqlException
        if (ex.InnerException != null)
        {
            var innerEx = ex.InnerException;
            var constraintNameProp = innerEx.GetType().GetProperty("ConstraintName");
            if (constraintNameProp != null)
            {
                var constraintName = constraintNameProp.GetValue(innerEx) as string;
                if (constraintName?.Contains("UQ_UserScopeAssignment", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }

            var sqlStateProp = innerEx.GetType().GetProperty("SqlState");
            if (sqlStateProp != null)
            {
                var sqlState = sqlStateProp.GetValue(innerEx) as string;
                if (sqlState == "23505") // PostgreSQL unique violation
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an exception is caused by a foreign key constraint violation.
    /// Uses multiple detection strategies for cross-database compatibility.
    /// </summary>
    private static bool IsForeignKeyViolation(Exception ex)
    {
        // Check exception message and inner exception message
        var message = ex.Message;
        var innerMessage = ex.InnerException?.Message ?? string.Empty;
        var fullMessage = $"{message} {innerMessage}";

        // Check for foreign key keywords
        if (fullMessage.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("FOREIGN KEY constraint", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("foreign_key_violation", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("23503", StringComparison.Ordinal)) // PostgreSQL error code
        {
            return true;
        }

        // Check inner exception properties using reflection for Npgsql/SqlException
        if (ex.InnerException != null)
        {
            var innerEx = ex.InnerException;
            var sqlStateProp = innerEx.GetType().GetProperty("SqlState");
            if (sqlStateProp != null)
            {
                var sqlState = sqlStateProp.GetValue(innerEx) as string;
                if (sqlState == "23503") // PostgreSQL foreign key violation
                {
                    return true;
                }
            }
        }

        return false;
    }
}
