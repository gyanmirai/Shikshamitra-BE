using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Volo.Abp.Threading;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
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
    private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
    private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
    private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
    private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly UserScopeAssignmentManager _assignmentManager;
    private readonly EmployeeOrganizationAssignmentSyncer _employeeOrganizationAssignmentSyncer;

    public UserScopeAppService(
        IUserScopeAssignmentRepository scopeRepository,
        IUserScopeService userScopeService,
        IIdentityUserRepository userRepository,
        IRepository<UserScopeAssignment, Guid> repository,
        ILogger<UserScopeAppService> logger,
        IRepository<AcademicYear, Guid> academicYearRepository,
        IRepository<EducationBoard, Guid> educationBoardRepository,
        IRepository<EducationMedium, Guid> educationMediumRepository,
        IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<Division, Guid> divisionRepository,
        UserScopeAssignmentManager assignmentManager,
        EmployeeOrganizationAssignmentSyncer employeeOrganizationAssignmentSyncer)
    {
        _scopeRepository = scopeRepository;
        _userScopeService = userScopeService;
        _userRepository = userRepository;
        _repository = repository;
        _logger = logger;
        _academicYearRepository = academicYearRepository;
        _educationBoardRepository = educationBoardRepository;
        _educationMediumRepository = educationMediumRepository;
        _educationMediumStageRepository = educationMediumStageRepository;
        _standardRepository = standardRepository;
        _divisionRepository = divisionRepository;
        _assignmentManager = assignmentManager;
        _employeeOrganizationAssignmentSyncer = employeeOrganizationAssignmentSyncer;
    }

    [Authorize(ShikshaMitraPermissions.UserScope.Assign)]
    public async Task<UserScopeAssignmentDto> AssignScopeAsync(CreateUserScopeAssignmentDto input)
    {
        try
        {
            _logger.LogInformation("Attempting to assign scope. UserId: {UserId}, RoleName: {RoleName}",
                input.UserId, input.RoleName);

            var assignment = await _assignmentManager.CreateAsync(
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
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Error assigning user scope. UserId: {UserId}, RoleName: {RoleName}, CorrelationId: {CorrelationId}",
                input.UserId, input.RoleName, correlationId);

            throw new BusinessException(UserScopeErrorCodes.AssignmentFailed)
                .WithData("CorrelationId", correlationId);
        }
    }

    [Authorize(ShikshaMitraPermissions.UserScope.BulkAssign)]
    public async Task<BulkScopeAssignmentResultDto> BulkAssignAsync(BulkScopeAssignmentDto input)
    {
        return await ReplaceEmployeeScopesAsync(input);
    }

    // The HR entry point checks the employee, draft ownership and delegation bounds first.
    internal async Task<BulkScopeAssignmentResultDto> ReplaceEmployeeScopesAsync(BulkScopeAssignmentDto input)
    {
        var correlationId = Guid.NewGuid();

        try
        {
            _logger.LogInformation(
                "Attempting transactional bulk scope assignment with ACADEMIC_YEAR_SCOPED_OVERWRITE strategy. UserCount: {UserCount}, CorrelationId: {CorrelationId}",
                input.UserIds?.Count ?? 0, correlationId);

            // Validate input
            if (input.UserIds == null || !input.UserIds.Any())
            {
                throw new BusinessException(
                    code: UserScopeErrorCodes.NoUsers,
                    message: "At least one user ID must be provided."
                );
            }

            if (!input.AcademicYearId.HasValue || input.AcademicYearId.Value == Guid.Empty)
            {
                throw InvalidBulkScopeInput("Select an academic year before saving academic permissions.");
            }
            var targetAcademicYearId = input.AcademicYearId.Value;
            var scopeRows = NormalizeBulkScopeRows(input);

            // Validate all users exist before processing - OPTIMIZED: Batch validation
            var distinctUserIds = input.UserIds.Distinct().ToList();
            var missingUserIds = new List<Guid>();

            foreach (var userId in distinctUserIds)
            {
                if (await _userRepository.FindAsync(userId) == null)
                {
                    missingUserIds.Add(userId);
                }
            }

            if (missingUserIds.Any())
            {
                _logger.LogWarning(
                    "Bulk assignment failed - users not found. MissingUserIds: [{MissingUserIds}], CorrelationId: {CorrelationId}",
                    string.Join(", ", missingUserIds), correlationId);

                throw new BusinessException(UserScopeErrorCodes.UsersNotFound)
                    .WithData("UserIds", string.Join(", ", missingUserIds))
                    .WithData("CorrelationId", correlationId);
            }

            // Scope rows are already compressed hierarchy paths from the UI. Explicit clear mode
            // deletes the target academic-year slice and intentionally creates no replacement rows.
            var totalCombinations = distinctUserIds.Count * scopeRows.Count;

            _logger.LogInformation(
                "Prepared {ScopeRowCount} scope rows for {UserCount} users. Total assignments to create: {TotalAssignments}, CorrelationId: {CorrelationId}",
                scopeRows.Count, distinctUserIds.Count, totalCombinations, correlationId);

            var deletedCount = 0;
            var createdCount = 0;
            var affectedUserIds = new HashSet<Guid>(distinctUserIds);

            try
            {
                var newAssignments = new List<UserScopeAssignment>(totalCombinations);

                foreach (var userId in distinctUserIds)
                {
                    foreach (var scope in scopeRows)
                    {
                        var assignment = await _assignmentManager.CreateAsync(
                            userId,
                            null,
                            targetAcademicYearId,
                            scope.EducationBoardId,
                            scope.EducationMediumId,
                            scope.EducationMediumStageId,
                            scope.StandardId,
                            scope.DivisionId,
                            input.Description,
                            checkDuplicate: false
                        );

                        newAssignments.Add(assignment);
                    }
                }

                createdCount = newAssignments.Count;

                // STEP 1: Replace only the academic-year slice being edited.
                _logger.LogInformation(
                    "Step 1/2: Deleting existing scope assignments for {UserCount} users in academic year {AcademicYearId}. CorrelationId: {CorrelationId}",
                    distinctUserIds.Count, targetAcademicYearId, correlationId);

                var existingQuery = (await _scopeRepository.GetQueryableAsync())
                    .Where(x =>
                        x.UserId.HasValue &&
                        distinctUserIds.Contains(x.UserId.Value) &&
                        x.AcademicYearId.HasValue &&
                        x.AcademicYearId.Value == targetAcademicYearId);

                var existingAssignments = await AsyncExecuter.ToListAsync(existingQuery);
                deletedCount = existingAssignments.Count;

                if (deletedCount > 0)
                {
                    await _scopeRepository.DeleteManyAsync(existingAssignments, autoSave: false);
                }

                _logger.LogInformation(
                    "Deleted {DeletedCount} existing assignments. CorrelationId: {CorrelationId}",
                    deletedCount, correlationId);

                // STEP 2: Create all new assignments - OPTIMIZED: Batch creation
                _logger.LogInformation(
                    "Step 2/2: Creating {TotalCombinations} new scope assignments. CorrelationId: {CorrelationId}",
                    totalCombinations, correlationId);

                if (createdCount > 0)
                {
                    await _scopeRepository.InsertManyAsync(newAssignments, autoSave: false);
                }

                // Save all changes at once - this will fail if any constraint is violated
                // Both deletions and insertions are in the same transaction
                await CurrentUnitOfWork.SaveChangesAsync();

                await _employeeOrganizationAssignmentSyncer.SyncFromAcademicPermissionsAsync(
                    distinctUserIds,
                    [targetAcademicYearId]);
                await CurrentUnitOfWork.SaveChangesAsync();

                // Invalidate cache for all affected users (only after successful save) - OPTIMIZED: Parallel
                _logger.LogDebug(
                    "Invalidating cache for {UserCount} users. CorrelationId: {CorrelationId}",
                    affectedUserIds.Count, correlationId);

                var cacheInvalidationTasks = affectedUserIds
                    .Select(userId => _userScopeService.InvalidateUserScopeCacheAsync(userId))
                    .ToList();

                await Task.WhenAll(cacheInvalidationTasks);

                _logger.LogInformation(
                    "Bulk scope assignment completed successfully. Deleted: {Deleted}, Created: {Created}, Users affected: {UserCount}, CorrelationId: {CorrelationId}",
                    deletedCount, createdCount, affectedUserIds.Count, correlationId);

                return new BulkScopeAssignmentResultDto
                {
                    DeletedAssignments = deletedCount,
                    AssignmentsCreated = createdCount,
                    TotalCombinations = totalCombinations,
                    AffectedUserIds = affectedUserIds.ToList(),
                    Message = input.ClearExisting
                        ? $"Successfully cleared scope assignments for {affectedUserIds.Count} users in one academic year. Deleted {deletedCount} old assignments."
                        : $"Successfully replaced scope assignments for {affectedUserIds.Count} users in one academic year. Deleted {deletedCount} old assignments and created {createdCount} new assignments."
                };
            }
            catch (Exception dbEx) when (IsConstraintViolationException(dbEx))
            {
                if (IsForeignKeyViolation(dbEx))
                {
                    _logger.LogWarning(dbEx,
                        "Bulk assignment failed due to invalid references. CorrelationId: {CorrelationId}",
                        correlationId);

                    throw new BusinessException(UserScopeErrorCodes.InvalidReferences)
                        .WithData("CorrelationId", correlationId);
                }

                throw;
            }
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

            throw new BusinessException(UserScopeErrorCodes.BulkAssignFailed)
                .WithData("CorrelationId", correlationId);
        }
    }

    private static List<BulkScopeAssignmentScopeDto> NormalizeBulkScopeRows(BulkScopeAssignmentDto input)
    {
        var scopes = input.Scopes;

        if (input.ClearExisting)
        {
            if (scopes is { Count: > 0 })
            {
                throw InvalidBulkScopeInput("ClearExisting can only be used with an empty scopes array.");
            }

            return [];
        }

        if (scopes == null || scopes.Count == 0)
        {
            throw InvalidBulkScopeInput(
                "At least one scope row must be provided when ClearExisting is false. Use an empty scope row for full academic-year access.");
        }

        if (scopes.Any(scope => scope == null))
        {
            throw InvalidBulkScopeInput("Scope rows cannot be null.");
        }

        if (scopes.Any(HasEmptyGuid))
        {
            throw InvalidBulkScopeInput("Scope IDs cannot be empty GUIDs.");
        }

        return scopes
            .GroupBy(x => new
            {
                x.EducationBoardId,
                x.EducationMediumId,
                x.EducationMediumStageId,
                x.StandardId,
                x.DivisionId
            })
            .Select(x => x.First())
            .ToList();
    }

    private static bool HasEmptyGuid(BulkScopeAssignmentScopeDto scope) =>
        scope.EducationBoardId == Guid.Empty ||
        scope.EducationMediumId == Guid.Empty ||
        scope.EducationMediumStageId == Guid.Empty ||
        scope.StandardId == Guid.Empty ||
        scope.DivisionId == Guid.Empty;

    private static BusinessException InvalidBulkScopeInput(string message) =>
        new BusinessException(code: UserScopeErrorCodes.InvalidInput, message: message)
            .WithData("Reason", message);

    [Authorize(ShikshaMitraPermissions.UserScope.Revoke)]
    public async Task RevokeScopeAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Attempting to revoke scope assignment {AssignmentId}", id);

            var assignment = await _repository.FindAsync(id);
            if (assignment == null)
            {
                throw new BusinessException(UserScopeErrorCodes.AssignmentNotFound)
                    .WithData("AssignmentId", id);
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
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Error revoking scope assignment {AssignmentId}, CorrelationId: {CorrelationId}", id, correlationId);
            throw new BusinessException(UserScopeErrorCodes.RevokeFailed)
                .WithData("CorrelationId", correlationId);
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

            // Get user info - throw 404 if user doesn't exist
            var user = await _userRepository.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found when retrieving scope", userId);
                throw new EntityNotFoundException(typeof(IdentityUser), userId);
            }

            // Get user's scope context
            // Note: Users without scope assignments will get an empty scope (this is valid!)
            var scopeContext = await _userScopeService.GetUserScopeAsync(userId);

            // Get all assignments (may be empty for users without assignments)
            var assignments = await _scopeRepository.GetUserAssignmentsAsync(userId);

            _logger.LogDebug(
                "Retrieved scope for user {UserId}. IsAdmin: {IsAdmin}, AssignmentCount: {Count}",
                userId, scopeContext.IsAdmin, assignments.Count);

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
        catch (EntityNotFoundException)
        {
            // Re-throw entity not found (404 response)
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Error getting scope for user {UserId}, CorrelationId: {CorrelationId}", userId, correlationId);
            throw new BusinessException(UserScopeErrorCodes.GetScopeFailed)
                .WithData("CorrelationId", correlationId);
        }
    }

    [Authorize]
    public async Task<UserScopeHierarchyDto> GetUserScopeHierarchyAsync(Guid userId, Guid? academicYearId = null)
    {
        if (CurrentUser.Id != userId)
            await AuthorizationService.CheckAsync(ShikshaMitraPermissions.UserScope.View);
        return await ReadEmployeeScopeHierarchyAsync(userId, academicYearId);
    }

    internal async Task<UserScopeHierarchyDto> ReadEmployeeScopeHierarchyAsync(Guid userId, Guid? academicYearId)
    {
        var cancellation = LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>().Token;
        var timer = Stopwatch.StartNew();
        var correlation = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        try
        {
            if (!CurrentUser.IsAuthenticated) throw new AbpAuthorizationException();
            var user = await _userRepository.FindAsync(userId, includeDetails: false, cancellationToken: cancellation);
            if (user == null || user.TenantId != CurrentTenant.Id)
                throw new EntityNotFoundException(typeof(IdentityUser), userId);
            if (academicYearId.HasValue && !await AsyncExecuter.AnyAsync(
                (await _academicYearRepository.GetQueryableAsync()).Where(year => year.Id == academicYearId.Value), cancellation))
                throw new BusinessException(UserScopeErrorCodes.AcademicYearNotFound);
            var identityMs = timer.Elapsed.TotalMilliseconds;
            var scopeContext = await _userScopeService.GetUserScopeAsync(userId, cancellation);
            var scopeMs = timer.Elapsed.TotalMilliseconds - identityMs;
            var hierarchy = await BuildHierarchyAsync(scopeContext, academicYearId, cancellation);
            _logger.LogInformation("ScopeHierarchy CorrelationId={CorrelationId} IdentityMs={IdentityMs} ScopeMs={ScopeMs} HierarchyMs={HierarchyMs} TotalMs={TotalMs}",
                correlation, identityMs, scopeMs, timer.Elapsed.TotalMilliseconds - identityMs - scopeMs, timer.Elapsed.TotalMilliseconds);
            return new UserScopeHierarchyDto
            {
                UserId = userId, UserName = user.UserName ?? string.Empty, Email = user.Email,
                IsAdmin = scopeContext.IsAdmin, AccessibleHierarchy = hierarchy,
                FromCache = false, GeneratedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (AbpAuthorizationException) { throw; }
        catch (EntityNotFoundException) { throw; }
        catch (BusinessException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScopeHierarchy failed CorrelationId={CorrelationId}", correlation);
            throw new BusinessException(UserScopeErrorCodes.GetHierarchyFailed).WithData("CorrelationId", correlation);
        }
    }

    /// <summary>
    /// Builds the complete hierarchical structure with access indicators.
    /// Optimization: Stops loading nested entities when full access is detected at any level.
    /// </summary>
    /// <param name="academicYearId">Optional: Filter to specific academic year</param>
    private async Task<List<AcademicYearScopeNode>> BuildHierarchyAsync(
        UserScopeContext scopeContext,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        // If admin, user has access to everything
        var isAdmin = scopeContext.IsAdmin;

        // Extract accessible IDs from scope context
        var accessibleBoardIds = new HashSet<Guid>(scopeContext.BoardIds ?? new List<Guid>());
        var accessibleMediumIds = new HashSet<Guid>(scopeContext.MediumIds ?? new List<Guid>());
        var accessibleMediumStageIds = new HashSet<Guid>(scopeContext.MediumStageIds ?? new List<Guid>());
        var accessibleStandardIds = new HashSet<Guid>(scopeContext.StandardIds ?? new List<Guid>());
        var accessibleDivisionIds = new HashSet<Guid>(scopeContext.DivisionIds ?? new List<Guid>());

        // Analyze assignments to determine "full access" at each level
        var fullAccessAnalysis = new FullAccessAnalysis
        {
            AcademicYearFullBoardAccess = scopeContext.WildcardBoardAcademicYearIds.ToHashSet(),
            BoardFullMediumAccess = scopeContext.WildcardMediumBoardIds.ToHashSet(),
            MediumFullMediumStageAccess = scopeContext.WildcardMediumStageMediumIds.ToHashSet(),
            MediumStageFullStandardAccess = scopeContext.WildcardStandardMediumStageIds.ToHashSet(),
            StandardFullDivisionAccess = scopeContext.WildcardDivisionStandardIds.ToHashSet()
        };

        // Load academic years for the tenant (with optional filter)
        var academicYearsQuery = await _academicYearRepository.GetQueryableAsync();

        if (academicYearId.HasValue)
        {
            // Filter to specific academic year
            academicYearsQuery = academicYearsQuery.Where(ay => ay.Id == academicYearId.Value);
        }

        var academicYears = await AsyncExecuter.ToListAsync(academicYearsQuery
            .Select(year => new { year.Id, year.Name, year.StartDate }), cancellationToken);

        // Six bounded projections. No navigation entities are materialized and no query runs inside a node loop.
        // Keep Enumerable.Contains explicit: newer compilers prefer span overloads that EF cannot interpret.
        var yearIds = academicYears.Where(year => !isAdmin && !scopeContext.HasWildcardAcademicYear &&
            !fullAccessAnalysis.AcademicYearFullBoardAccess.Contains(year.Id)).Select(year => year.Id).ToArray();
        var boardRows = await AsyncExecuter.ToListAsync((await _educationBoardRepository.GetQueryableAsync())
            .Where(board => Enumerable.Contains(yearIds, board.AcademicYearId))
            .Select(board => new { board.Id, board.AcademicYearId, board.Type }), cancellationToken);
        var boardIds = boardRows.Where(board => !fullAccessAnalysis.BoardFullMediumAccess.Contains(board.Id)).Select(board => board.Id).ToArray();
        var mediumRows = await AsyncExecuter.ToListAsync((await _educationMediumRepository.GetQueryableAsync())
            .Where(medium => Enumerable.Contains(boardIds, medium.EducationBoardId))
            .Select(medium => new { medium.Id, medium.EducationBoardId, medium.Name }), cancellationToken);
        var mediumIds = mediumRows.Where(medium => !fullAccessAnalysis.MediumFullMediumStageAccess.Contains(medium.Id)).Select(medium => medium.Id).ToArray();
        var stageRows = await AsyncExecuter.ToListAsync((await _educationMediumStageRepository.GetQueryableAsync())
            .Where(stage => Enumerable.Contains(mediumIds, stage.EducationMediumId))
            .Select(stage => new { stage.Id, stage.EducationMediumId, stage.Name, stage.SortOrder }), cancellationToken);
        var stageIds = stageRows.Where(stage => !fullAccessAnalysis.MediumStageFullStandardAccess.Contains(stage.Id)).Select(stage => stage.Id).ToArray();
        var standardRows = await AsyncExecuter.ToListAsync((await _standardRepository.GetQueryableAsync())
            .Where(standard => Enumerable.Contains(stageIds, standard.EducationMediumStageId))
            .Select(standard => new { standard.Id, standard.EducationMediumStageId, standard.Name, standard.SortOrder }), cancellationToken);
        var standardIds = standardRows.Where(standard => !fullAccessAnalysis.StandardFullDivisionAccess.Contains(standard.Id)).Select(standard => standard.Id).ToArray();
        var divisionRows = await AsyncExecuter.ToListAsync((await _divisionRepository.GetQueryableAsync())
            .Where(division => Enumerable.Contains(standardIds, division.StandardId))
            .Select(division => new { division.Id, division.StandardId, division.Name, division.RoomNumber, division.SortOrder }), cancellationToken);
        var boardsByYear = boardRows.ToLookup(board => board.AcademicYearId);
        var mediumsByBoard = mediumRows.ToLookup(medium => medium.EducationBoardId);
        var stagesByMedium = stageRows.ToLookup(stage => stage.EducationMediumId);
        var standardsByStage = standardRows.ToLookup(standard => standard.EducationMediumStageId);
        var divisionsByStandard = divisionRows.ToLookup(division => division.StandardId);

        var hierarchy = new List<AcademicYearScopeNode>();

        foreach (var academicYear in academicYears.OrderByDescending(ay => ay.StartDate))
        {
            var yearNode = new AcademicYearScopeNode
            {
                AcademicYearId = academicYear.Id,
                AcademicYearName = academicYear.Name
            };

            // Check if user has full board access for this academic year
            var yearFullAccess = fullAccessAnalysis.AcademicYearFullBoardAccess.Contains(academicYear.Id);
            yearNode.HasFullBoardAccess = isAdmin || scopeContext.HasWildcardAcademicYear || yearFullAccess;

            // OPTIMIZATION: If user has full board access, don't load nested hierarchy
            // Frontend will understand all boards (and their children) are accessible
            if (yearNode.HasFullBoardAccess)
            {
                _logger.LogDebug(
                    "User has full board access for academic year {AcademicYearId}. Skipping nested hierarchy load.",
                    academicYear.Id);
                hierarchy.Add(yearNode);
                continue; // Skip loading boards and nested entities
            }

            // Load all boards for this academic year
            var boards = boardsByYear[academicYear.Id];

            foreach (var board in boards.OrderBy(b => b.Type))
            {
                var boardNode = new BoardScopeNode
                {
                    BoardId = board.Id,
                    BoardName = board.Type.ToString(),
                    HasAccess = isAdmin || accessibleBoardIds.Contains(board.Id)
                };

                // Check if user has full medium access for this board
                var boardFullMediumAccess = fullAccessAnalysis.BoardFullMediumAccess.Contains(board.Id);
                boardNode.HasFullMediumAccess = isAdmin || boardFullMediumAccess;

                // OPTIMIZATION: If user has full medium access, don't load nested hierarchy
                if (boardNode.HasFullMediumAccess)
                {
                    _logger.LogDebug(
                        "User has full medium access for board {BoardId}. Skipping nested hierarchy load.",
                        board.Id);
                    yearNode.Boards.Add(boardNode);
                    continue; // Skip loading mediums and nested entities
                }

                // Load all mediums for this board
                var mediums = mediumsByBoard[board.Id];

                foreach (var medium in mediums.OrderBy(m => m.Name))
                {
                    var mediumNode = new MediumScopeNode
                    {
                        MediumId = medium.Id,
                        MediumName = medium.Name,
                        HasAccess = isAdmin || accessibleMediumIds.Contains(medium.Id)
                    };

                    // Check if user has full medium stage access for this medium
                    var mediumFullMediumStageAccess = fullAccessAnalysis.MediumFullMediumStageAccess.Contains(medium.Id);
                    mediumNode.HasFullMediumStageAccess = isAdmin || mediumFullMediumStageAccess;

                    // OPTIMIZATION: If user has full medium stage access, don't load nested hierarchy
                    if (mediumNode.HasFullMediumStageAccess)
                    {
                        _logger.LogDebug(
                            "User has full medium stage access for medium {MediumId}. Skipping nested hierarchy load.",
                            medium.Id);
                        boardNode.Mediums.Add(mediumNode);
                        continue; // Skip loading medium stages and nested entities
                    }

                    // Load all medium stages for this medium
                    var mediumStages = stagesByMedium[medium.Id];

                    foreach (var mediumStage in mediumStages.OrderBy(ms => ms.SortOrder).ThenBy(ms => ms.Name).ThenBy(ms => ms.Id))
                    {
                        var mediumStageNode = new MediumStageScopeNode
                        {
                            MediumStageId = mediumStage.Id,
                            MediumStageName = mediumStage.Name,
                            HasAccess = isAdmin || accessibleMediumStageIds.Contains(mediumStage.Id)
                        };

                        // Check if user has full standard access for this medium stage
                        var mediumStageFullStandardAccess = fullAccessAnalysis.MediumStageFullStandardAccess.Contains(mediumStage.Id);
                        mediumStageNode.HasFullStandardAccess = isAdmin || mediumStageFullStandardAccess;

                        // OPTIMIZATION: If user has full standard access, don't load nested hierarchy
                        if (mediumStageNode.HasFullStandardAccess)
                        {
                            _logger.LogDebug(
                                "User has full standard access for medium stage {MediumStageId}. Skipping nested hierarchy load.",
                                mediumStage.Id);
                            mediumNode.MediumStages.Add(mediumStageNode);
                            continue; // Skip loading standards and nested entities
                        }

                        // Load all standards for this medium stage
                        var standards = standardsByStage[mediumStage.Id];

                        foreach (var standard in standards.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ThenBy(s => s.Id))
                        {
                            var standardNode = new StandardScopeNode
                            {
                                StandardId = standard.Id,
                                StandardName = standard.Name,
                                HasAccess = isAdmin || accessibleStandardIds.Contains(standard.Id)
                            };

                            // Check if user has full division access for this standard
                            var standardFullDivisionAccess = fullAccessAnalysis.StandardFullDivisionAccess.Contains(standard.Id);
                            standardNode.HasFullDivisionAccess = isAdmin || standardFullDivisionAccess;

                            // OPTIMIZATION: If user has full division access, don't load divisions
                            if (standardNode.HasFullDivisionAccess)
                            {
                                _logger.LogDebug(
                                    "User has full division access for standard {StandardId}. Skipping divisions load.",
                                    standard.Id);
                                mediumStageNode.Standards.Add(standardNode);
                                continue; // Skip loading divisions
                            }

                            // Load all divisions for this standard
                            var divisions = divisionsByStandard[standard.Id];

                            foreach (var division in divisions.OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ThenBy(d => d.Id))
                            {
                                var divisionNode = new DivisionScopeNode
                                {
                                    DivisionId = division.Id,
                                    DivisionName = division.Name,
                                    HasAccess = isAdmin || accessibleDivisionIds.Contains(division.Id),
                                    RoomNumber = division.RoomNumber
                                };

                                standardNode.Divisions.Add(divisionNode);
                            }

                            mediumStageNode.Standards.Add(standardNode);
                        }

                        mediumNode.MediumStages.Add(mediumStageNode);
                    }

                    boardNode.Mediums.Add(mediumNode);
                }

                yearNode.Boards.Add(boardNode);
            }

            hierarchy.Add(yearNode);
        }

        return hierarchy;
    }

    /// <summary>
    /// Helper class to track "full access" flags at different hierarchy levels.
    /// </summary>
    private class FullAccessAnalysis
    {
        public HashSet<Guid> AcademicYearFullBoardAccess { get; init; } = new();
        public HashSet<Guid> BoardFullMediumAccess { get; init; } = new();
        public HashSet<Guid> MediumFullMediumStageAccess { get; init; } = new();
        public HashSet<Guid> MediumStageFullStandardAccess { get; init; } = new();
        public HashSet<Guid> StandardFullDivisionAccess { get; init; } = new();
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
