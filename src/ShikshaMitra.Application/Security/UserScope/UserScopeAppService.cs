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
    public async Task<BulkScopeAssignmentResultDto> BulkAssignAsync(BulkScopeAssignmentDto input)
    {
        var correlationId = Guid.NewGuid();

        try
        {
            _logger.LogInformation(
                "Attempting transactional bulk scope assignment with OVERWRITE strategy. UserCount: {UserCount}, CorrelationId: {CorrelationId}",
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

            // Generate all combinations of scope parameters
            var scopeCombinations = GenerateScopeCombinations(input);
            var totalCombinations = distinctUserIds.Count * scopeCombinations.Count;

            _logger.LogInformation(
                "Generated {CombinationCount} scope combinations for {UserCount} users. Total assignments to create: {TotalAssignments}, CorrelationId: {CorrelationId}",
                scopeCombinations.Count, distinctUserIds.Count, totalCombinations, correlationId);

            var deletedCount = 0;
            var createdAssignments = new List<UserScopeAssignment>();
            var affectedUserIds = new HashSet<Guid>();

            try
            {
                // STEP 1: Delete all existing scope assignments for the specified users
                _logger.LogInformation(
                    "Step 1/2: Deleting existing scope assignments for {UserCount} users. CorrelationId: {CorrelationId}",
                    distinctUserIds.Count, correlationId);

                foreach (var userId in distinctUserIds)
                {
                    var existingAssignments = await _scopeRepository.GetUserAssignmentsAsync(userId);
                    foreach (var assignment in existingAssignments)
                    {
                        await _scopeRepository.DeleteAsync(assignment, autoSave: false);
                        deletedCount++;
                    }
                }

                _logger.LogInformation(
                    "Deleted {DeletedCount} existing assignments. CorrelationId: {CorrelationId}",
                    deletedCount, correlationId);

                // STEP 2: Create all new assignments
                _logger.LogInformation(
                    "Step 2/2: Creating {TotalCombinations} new scope assignments. CorrelationId: {CorrelationId}",
                    totalCombinations, correlationId);

                foreach (var userId in distinctUserIds)
                {
                    foreach (var scope in scopeCombinations)
                    {
                        var assignment = new UserScopeAssignment(
                            GuidGenerator.Create(),
                            CurrentTenant.Id,
                            userId,
                            null,
                            scope.AcademicYearId,
                            scope.EducationBoardId,
                            scope.EducationMediumId,
                            scope.EducationMediumStageId,
                            scope.StandardId,
                            scope.DivisionId,
                            input.Description
                        );

                        await _scopeRepository.InsertAsync(assignment, autoSave: false);
                        createdAssignments.Add(assignment);
                        affectedUserIds.Add(userId);
                    }
                }

                // Save all changes at once - this will fail if any constraint is violated
                // Both deletions and insertions are in the same transaction
                await CurrentUnitOfWork.SaveChangesAsync();

                // Invalidate cache for all affected users (only after successful save)
                foreach (var userId in affectedUserIds)
                {
                    await _userScopeService.InvalidateUserScopeCacheAsync(userId);
                }

                _logger.LogInformation(
                    "Bulk scope assignment completed successfully. Deleted: {Deleted}, Created: {Created}, Users affected: {UserCount}, CorrelationId: {CorrelationId}",
                    deletedCount, createdAssignments.Count, affectedUserIds.Count, correlationId);

                return new BulkScopeAssignmentResultDto
                {
                    DeletedAssignments = deletedCount,
                    AssignmentsCreated = createdAssignments.Count,
                    TotalCombinations = totalCombinations,
                    AffectedUserIds = affectedUserIds.ToList(),
                    Message = $"Successfully replaced scope assignments for {affectedUserIds.Count} users. Deleted {deletedCount} old assignments and created {createdAssignments.Count} new assignments."
                };
            }
            catch (Exception dbEx) when (IsConstraintViolationException(dbEx))
            {
                if (IsForeignKeyViolation(dbEx))
                {
                    _logger.LogWarning(dbEx,
                        "Bulk assignment failed due to invalid references. CorrelationId: {CorrelationId}",
                        correlationId);

                    throw new UserFriendlyException(
                        $"One or more referenced entities (Standard, Division, Board, Medium, etc.) do not exist. No changes were made. Please verify all IDs are valid. (Ref: {correlationId})",
                        code: "UserScope:InvalidReferences"
                    );
                }

                throw;
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
                "Error during bulk scope assignment. CorrelationId: {CorrelationId}",
                correlationId);

            throw new UserFriendlyException(
                $"An error occurred during bulk assignment. No changes were made. Please contact support with reference: {correlationId}",
                code: "UserScope:BulkAssignFailed"
            );
        }
    }

    /// <summary>
    /// Generates all combinations (cartesian product) of scope parameters from the input.
    /// </summary>
    private List<ScopeParameterCombination> GenerateScopeCombinations(BulkScopeAssignmentDto input)
    {
        var combinations = new List<ScopeParameterCombination>();

        // Start with a single empty combination
        var currentCombinations = new List<ScopeParameterCombination> { new ScopeParameterCombination() };

        // Helper function to multiply combinations with a list of IDs
        void MultiplyWithIds(List<Guid>? ids, Action<ScopeParameterCombination, Guid?> setter)
        {
            if (ids == null || !ids.Any())
            {
                // If no IDs provided, add null to each combination
                foreach (var combo in currentCombinations)
                {
                    setter(combo, null);
                }
            }
            else
            {
                // Create new combinations for each ID
                var newCombinations = new List<ScopeParameterCombination>();
                foreach (var combo in currentCombinations)
                {
                    foreach (var id in ids)
                    {
                        var newCombo = combo.Clone();
                        setter(newCombo, id);
                        newCombinations.Add(newCombo);
                    }
                }
                currentCombinations = newCombinations;
            }
        }

        // Multiply combinations for each hierarchical level
        MultiplyWithIds(input.AcademicYearIds, (combo, id) => combo.AcademicYearId = id);
        MultiplyWithIds(input.EducationBoardIds, (combo, id) => combo.EducationBoardId = id);
        MultiplyWithIds(input.EducationMediumIds, (combo, id) => combo.EducationMediumId = id);
        MultiplyWithIds(input.EducationMediumStageIds, (combo, id) => combo.EducationMediumStageId = id);
        MultiplyWithIds(input.StandardIds, (combo, id) => combo.StandardId = id);
        MultiplyWithIds(input.DivisionIds, (combo, id) => combo.DivisionId = id);

        return currentCombinations;
    }

    /// <summary>
    /// Helper class to represent a single scope parameter combination.
    /// </summary>
    private class ScopeParameterCombination
    {
        public Guid? AcademicYearId { get; set; }
        public Guid? EducationBoardId { get; set; }
        public Guid? EducationMediumId { get; set; }
        public Guid? EducationMediumStageId { get; set; }
        public Guid? StandardId { get; set; }
        public Guid? DivisionId { get; set; }

        public ScopeParameterCombination Clone()
        {
            return new ScopeParameterCombination
            {
                AcademicYearId = this.AcademicYearId,
                EducationBoardId = this.EducationBoardId,
                EducationMediumId = this.EducationMediumId,
                EducationMediumStageId = this.EducationMediumStageId,
                StandardId = this.StandardId,
                DivisionId = this.DivisionId
            };
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
