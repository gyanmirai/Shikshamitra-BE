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

            // Check if assignment already exists
            var exists = await _scopeRepository.ExistsAsync(
                input.UserId,
                input.RoleName,
                input.AcademicYearId,
                input.EducationBoardId,
                input.EducationMediumId,
                input.EducationMediumStageId,
                input.StandardId,
                input.DivisionId
            );

            if (exists)
            {
                throw new UserFriendlyException(
                    "This scope assignment already exists for the user/role with the specified parameters.",
                    code: "UserScope:AlreadyExists"
                );
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

            await _scopeRepository.InsertAsync(assignment);

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
            throw; // Re-throw user-friendly exceptions as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user scope. UserId: {UserId}, RoleName: {RoleName}",
                input.UserId, input.RoleName);

            // Check for foreign key violations
            if (ex.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("violates foreign key", StringComparison.OrdinalIgnoreCase) ||
                ex.InnerException?.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new UserFriendlyException(
                    "One or more referenced entities (Standard, Division, Board, Medium, etc.) do not exist. Please verify all IDs are valid.",
                    code: "UserScope:ForeignKeyViolation"
                );  
            }

            throw new UserFriendlyException(
                $"An error occurred while assigning the scope: {ex.Message}",
                code: "UserScope:AssignmentFailed"
            );
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.BulkAssign)]
    public async Task<int> BulkAssignAsync(BulkScopeAssignmentDto input)
    {
        if (input.UserIds == null || !input.UserIds.Any())
        {
            throw new BusinessException(
                code: "UserScope:NoUsers",
                message: "At least one user ID must be provided."
            );
        }

        var assignmentsCreated = 0;

        foreach (var userId in input.UserIds)
        {
            // If specific divisions are provided, create one assignment per division
            if (input.DivisionIds != null && input.DivisionIds.Any())
            {
                foreach (var divisionId in input.DivisionIds)
                {
                    var exists = await _scopeRepository.ExistsAsync(
                        userId,
                        null,
                        input.AcademicYearId,
                        input.EducationBoardId,
                        input.EducationMediumId,
                        input.EducationMediumStageId,
                        input.StandardId,
                        divisionId
                    );

                    if (!exists)
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
                    }
                }
            }
            else
            {
                // Create single assignment with DivisionId = null (all divisions)
                var exists = await _scopeRepository.ExistsAsync(
                    userId,
                    null,
                    input.AcademicYearId,
                    input.EducationBoardId,
                    input.EducationMediumId,
                    input.EducationMediumStageId,
                    input.StandardId,
                    null
                );

                if (!exists)
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
                }
            }

            // Invalidate cache for each user
            await _userScopeService.InvalidateUserScopeCacheAsync(userId);
        }

        return assignmentsCreated;
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
            _logger.LogError(ex, "Error revoking scope assignment {AssignmentId}", id);
            throw new UserFriendlyException(
                $"An error occurred while revoking the scope assignment: {ex.Message}",
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
            _logger.LogError(ex, "Error getting scope for user {UserId}", userId);
            throw new UserFriendlyException(
                $"An error occurred while retrieving user scope: {ex.Message}",
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

        var assignments = query.ToList();
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
}
