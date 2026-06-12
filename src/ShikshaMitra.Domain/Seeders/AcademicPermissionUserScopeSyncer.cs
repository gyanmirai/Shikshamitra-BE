using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Seeders;

public class AcademicPermissionUserScopeSyncer(
	IRepository<AcademicYearPermission, Guid> academicYearPermissionRepository,
	IRepository<EducationBoardPermission, Guid> educationBoardPermissionRepository,
	IRepository<EducationMediumPermission, Guid> educationMediumPermissionRepository,
	IRepository<EducationMediumStagePermission, Guid> educationMediumStagePermissionRepository,
	IRepository<StandardPermission, Guid> standardPermissionRepository,
	IRepository<DivisionPermission, Guid> divisionPermissionRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<UserScopeAssignment, Guid> userScopeRepository,
	IUserScopeService userScopeService,
	ILogger<AcademicPermissionUserScopeSyncer> logger)
	: DomainService
{
	public async Task<int> SyncAsync()
	{
		logger.LogInformation("[AcademicPermissionUserScopeSyncer] Starting sync.");
		var existingScopes = await userScopeRepository.GetListAsync();
		await DeactivateLegacySeededScopesAsync(existingScopes);
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded existing scopes. Count={ExistingScopeCount}",
			existingScopes.Count);

		var existingKeys = existingScopes
			.Where(x => x.IsActive && !x.IsDeleted && x.UserId.HasValue)
			.Select(ScopeKey.FromAssignment)
			.ToHashSet();

		var desiredScopes = await BuildDesiredScopesAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Built desired scopes. Count={DesiredScopeCount}",
			desiredScopes.Count);
		var changedUserIds = new HashSet<Guid>();
		var assignmentsToInsert = new List<UserScopeAssignment>();

		foreach (var desiredScope in desiredScopes.Distinct())
		{
			if (!existingKeys.Add(desiredScope))
			{
				continue;
			}

			assignmentsToInsert.Add(new UserScopeAssignment(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				userId: desiredScope.UserId,
				academicYearId: desiredScope.AcademicYearId,
				educationBoardId: desiredScope.EducationBoardId,
				educationMediumId: desiredScope.EducationMediumId,
				educationMediumStageId: desiredScope.EducationMediumStageId,
				standardId: desiredScope.StandardId,
				divisionId: desiredScope.DivisionId,
				description: "Derived from Academic Permissions."));
			changedUserIds.Add(desiredScope.UserId);
		}

		if (assignmentsToInsert.Count > 0)
		{
			await userScopeRepository.InsertManyAsync(assignmentsToInsert, autoSave: false);
		}
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Inserted derived scopes. Count={InsertedScopeCount}",
			assignmentsToInsert.Count);

		foreach (var userId in changedUserIds)
		{
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
		}

		return assignmentsToInsert.Count;
	}

	private async Task DeactivateLegacySeededScopesAsync(IEnumerable<UserScopeAssignment> existingScopes)
	{
		var legacyScopes = existingScopes
			.Where(x => x.IsActive && IsLegacySeededScope(x.Description))
			.ToList();

		foreach (var scope in legacyScopes)
		{
			scope.Deactivate();
			await userScopeRepository.UpdateAsync(scope, autoSave: false);
			if (scope.UserId.HasValue)
			{
				await userScopeService.InvalidateUserScopeCacheAsync(scope.UserId.Value);
			}
		}
	}

	private static bool IsLegacySeededScope(string? description) =>
		!string.IsNullOrWhiteSpace(description) &&
		(description.StartsWith("Seeded full organization scope", StringComparison.OrdinalIgnoreCase) ||
		 description.StartsWith("Seeded GSEB board scope", StringComparison.OrdinalIgnoreCase) ||
		 description.StartsWith("Seeded CBSE board scope", StringComparison.OrdinalIgnoreCase) ||
		 description.StartsWith("Seeded academic scope for linked student login", StringComparison.OrdinalIgnoreCase));

	private async Task<HashSet<ScopeKey>> BuildDesiredScopesAsync()
	{
		var scopes = new HashSet<ScopeKey>();

		var academicYearPermissions = await academicYearPermissionRepository.GetListAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded academic year permissions. Count={PermissionCount}",
			academicYearPermissions.Count);
		foreach (var permission in academicYearPermissions)
		{
			if (permission.UserId.HasValue)
			{
				scopes.Add(new ScopeKey(permission.UserId.Value, permission.AcademicYearId));
			}
		}

		var boards = (await educationBoardRepository.GetListAsync()).ToDictionary(x => x.Id);
		var boardPermissions = await educationBoardPermissionRepository.GetListAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded board permissions. Count={PermissionCount}",
			boardPermissions.Count);
		foreach (var permission in boardPermissions)
		{
			if (permission.UserId.HasValue && boards.TryGetValue(permission.EducationBoardId, out var board))
			{
				scopes.Add(new ScopeKey(permission.UserId.Value, board.AcademicYearId, board.Id));
			}
		}

		var mediums = (await educationMediumRepository.GetListAsync()).ToDictionary(x => x.Id);
		var mediumPermissions = await educationMediumPermissionRepository.GetListAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded medium permissions. Count={PermissionCount}",
			mediumPermissions.Count);
		foreach (var permission in mediumPermissions)
		{
			if (permission.UserId.HasValue &&
			    mediums.TryGetValue(permission.EducationMediumId, out var medium) &&
			    boards.TryGetValue(medium.EducationBoardId, out var board))
			{
				scopes.Add(new ScopeKey(permission.UserId.Value, board.AcademicYearId, board.Id, medium.Id));
			}
		}

		var stages = (await educationMediumStageRepository.GetListAsync()).ToDictionary(x => x.Id);
		var stagePermissions = await educationMediumStagePermissionRepository.GetListAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded stage permissions. Count={PermissionCount}",
			stagePermissions.Count);
		foreach (var permission in stagePermissions)
		{
			if (permission.UserId.HasValue &&
			    stages.TryGetValue(permission.EducationMediumStageId, out var stage) &&
			    mediums.TryGetValue(stage.EducationMediumId, out var medium) &&
			    boards.TryGetValue(medium.EducationBoardId, out var board))
			{
				scopes.Add(new ScopeKey(permission.UserId.Value, board.AcademicYearId, board.Id, medium.Id, stage.Id));
			}
		}

		var standardPermissions = await standardPermissionRepository.GetListAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded standard permissions. Count={PermissionCount}",
			standardPermissions.Count);
		var divisionPermissions = await divisionPermissionRepository.GetListAsync();
		logger.LogInformation(
			"[AcademicPermissionUserScopeSyncer] Loaded division permissions. Count={PermissionCount}",
			divisionPermissions.Count);
		var standards = standardPermissions.Count == 0 && divisionPermissions.Count == 0
			? new Dictionary<Guid, Standard>()
			: (await standardRepository.GetListAsync()).ToDictionary(x => x.Id);
		foreach (var permission in standardPermissions)
		{
			if (permission.UserId.HasValue &&
			    standards.TryGetValue(permission.StandardId, out var standard) &&
			    stages.TryGetValue(standard.EducationMediumStageId, out var stage) &&
			    mediums.TryGetValue(stage.EducationMediumId, out var medium) &&
			    boards.TryGetValue(medium.EducationBoardId, out var board))
			{
				scopes.Add(new ScopeKey(permission.UserId.Value, board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id));
			}
		}

		var divisions = divisionPermissions.Count == 0
			? new Dictionary<Guid, Division>()
			: (await divisionRepository.GetListAsync()).ToDictionary(x => x.Id);
		foreach (var permission in divisionPermissions)
		{
			if (permission.UserId.HasValue &&
			    divisions.TryGetValue(permission.DivisionId, out var division) &&
			    standards.TryGetValue(division.StandardId, out var standard) &&
			    stages.TryGetValue(standard.EducationMediumStageId, out var stage) &&
			    mediums.TryGetValue(stage.EducationMediumId, out var medium) &&
			    boards.TryGetValue(medium.EducationBoardId, out var board))
			{
				scopes.Add(new ScopeKey(permission.UserId.Value, board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, division.Id));
			}
		}

		return scopes;
	}

	private sealed record ScopeKey(
		Guid UserId,
		Guid? AcademicYearId = null,
		Guid? EducationBoardId = null,
		Guid? EducationMediumId = null,
		Guid? EducationMediumStageId = null,
		Guid? StandardId = null,
		Guid? DivisionId = null)
	{
		public static ScopeKey FromAssignment(UserScopeAssignment assignment) =>
			new(
				assignment.UserId!.Value,
				assignment.AcademicYearId,
				assignment.EducationBoardId,
				assignment.EducationMediumId,
				assignment.EducationMediumStageId,
				assignment.StandardId,
				assignment.DivisionId);
	}
}
