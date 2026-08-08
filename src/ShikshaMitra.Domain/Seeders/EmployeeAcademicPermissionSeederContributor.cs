using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class EmployeeAcademicPermissionSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	IEmployeeRepository employeeRepository,
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<EducationMediumStagePermission, Guid> educationMediumStagePermissionRepository,
	AcademicSetupPermissionManager academicSetupPermissionManager,
	ILogger<EmployeeAcademicPermissionSeederContributor> logger)
	: BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	public override async Task SeedAsync(DataSeedContext context)
	{
		var employees = (await employeeRepository.GetListAsync())
			.Where(x => x.UserId.HasValue)
			.ToList();
		if (employees.Count == 0)
		{
			logger.LogInformation("[EmployeeAcademicPermissionSeeder] No linked employees found for academic permission backfill.");
			return;
		}

		var assignmentResult = await EnsureEmployeeOrganizationAssignmentsAsync(employees);
		var assignments = assignmentResult.Assignments;
		var userIdByEmployeeId = employees.ToDictionary(x => x.Id, x => x.UserId!.Value);
		if (assignments.Count == 0)
		{
			logger.LogInformation(
				"[EmployeeAcademicPermissionSeeder] No employee organization assignments found. LinkedEmployees={EmployeeCount}",
				employees.Count);
			return;
		}

		var userIds = userIdByEmployeeId.Values.ToHashSet();
		var existingPermissions = await educationMediumStagePermissionRepository.GetListAsync(x =>
			x.UserId.HasValue &&
			userIds.Contains(x.UserId.Value));
		var requiredBoardByUserId = assignmentResult.RequiredBoardByEmployeeId
			.Where(x => userIdByEmployeeId.ContainsKey(x.Key))
			.ToDictionary(x => userIdByEmployeeId[x.Key], x => x.Value);
		var obsoletePermissions = existingPermissions
			.Where(x =>
				x.UserId.HasValue &&
				requiredBoardByUserId.TryGetValue(x.UserId.Value, out var requiredBoard) &&
				assignmentResult.BoardByStageId.TryGetValue(x.EducationMediumStageId, out var actualBoard) &&
				actualBoard != requiredBoard)
			.ToList();
		if (obsoletePermissions.Count > 0)
		{
			await educationMediumStagePermissionRepository.DeleteManyAsync(obsoletePermissions, autoSave: false);
			existingPermissions.RemoveAll(x => obsoletePermissions.Contains(x));
		}
		var existingKeys = existingPermissions
			.Select(x => (x.UserId!.Value, x.EducationMediumStageId))
			.ToHashSet();

		var permissionsToInsert = new List<EducationMediumStagePermission>();
		foreach (var assignment in assignments)
		{
			if (!userIdByEmployeeId.TryGetValue(assignment.EmployeeId, out var userId))
			{
				continue;
			}

			if (!existingKeys.Add((userId, assignment.EducationMediumStageId)))
			{
				continue;
			}

			permissionsToInsert.AddRange(academicSetupPermissionManager.CreateEducationMediumStagePermissions(
				userId,
				[assignment.EducationMediumStageId]));
		}

		if (existingPermissions.Count == 0 && permissionsToInsert.Count == 0)
		{
			throw new InvalidOperationException(
				$"[EmployeeAcademicPermissionSeeder] Found {assignments.Count} employee organization assignments for {employees.Count} linked employees, but no Academic Permission rows were created. Refusing to mark the seeder complete.");
		}

		if (permissionsToInsert.Count > 0)
		{
			await educationMediumStagePermissionRepository.InsertManyAsync(permissionsToInsert, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();

		logger.LogInformation(
			"[EmployeeAcademicPermissionSeeder] Synced stage permissions from employee organization assignments. LinkedEmployees={EmployeeCount}, Assignments={AssignmentCount}, AssignmentsInserted={AssignmentsInserted}, PermissionsRemoved={PermissionsRemoved}, PermissionsInserted={InsertedCount}",
			employees.Count,
			assignments.Count,
			assignmentResult.InsertedCount,
			obsoletePermissions.Count,
			permissionsToInsert.Count);
	}

	private async Task<EmployeeOrganizationAssignmentBackfillResult> EnsureEmployeeOrganizationAssignmentsAsync(IReadOnlyCollection<Employee> employees)
	{
		var profilesByEmail = HRSeederContributor.BuildSuratSeedEmployeeProfiles();
		var stages = (await educationMediumStageRepository.GetQueryableAsync())
			.OrderBy(x => x.Name)
			.Select(x => new SeededEmployeeAcademicScopePolicy.Stage(
				x.Id,
				x.Name,
				x.EducationMedium.EducationBoard.Type))
			.ToList();
		if (stages.Count == 0)
		{
			return new EmployeeOrganizationAssignmentBackfillResult(
				[],
				0,
				new Dictionary<Guid, EducationBoardType>(),
				new Dictionary<Guid, EducationBoardType>());
		}

		var seededEmployees = employees
			.Where(x => x.Email != null && profilesByEmail.ContainsKey(x.Email))
			.ToList();
		if (seededEmployees.Count == 0)
		{
			return new EmployeeOrganizationAssignmentBackfillResult(
				[],
				0,
				new Dictionary<Guid, EducationBoardType>(),
				new Dictionary<Guid, EducationBoardType>());
		}

		var employeeIds = seededEmployees.Select(x => x.Id).ToHashSet();
		var existing = await employeeOrganizationAssignmentRepository.GetListAsync(x =>
			employeeIds.Contains(x.EmployeeId));
		var boardByStageId = stages.ToDictionary(x => x.Id, x => x.BoardType);
		var requiredBoardByEmployeeId = seededEmployees
			.Where(x => x.Email != null && SeededEmployeeAcademicScopePolicy.GetRequiredBoard(x.Email).HasValue)
			.ToDictionary(
				x => x.Id,
				x => SeededEmployeeAcademicScopePolicy.GetRequiredBoard(x.Email!)!.Value);
		var obsoleteAssignments = existing
			.Where(x =>
				requiredBoardByEmployeeId.TryGetValue(x.EmployeeId, out var requiredBoard) &&
				boardByStageId.TryGetValue(x.EducationMediumStageId, out var actualBoard) &&
				actualBoard != requiredBoard)
			.ToList();
		if (obsoleteAssignments.Count > 0)
		{
			await employeeOrganizationAssignmentRepository.DeleteManyAsync(obsoleteAssignments, autoSave: false);
			existing.RemoveAll(x => obsoleteAssignments.Contains(x));
		}
		var existingKeys = existing
			.Select(x => (x.EmployeeId, x.EducationMediumStageId))
			.ToHashSet();

		var assignments = new List<EmployeeOrganizationAssignment>();
		foreach (var employee in seededEmployees)
		{
			if (employee.Email == null || !profilesByEmail.TryGetValue(employee.Email, out var profile))
			{
				continue;
			}

			foreach (var stageId in SeededEmployeeAcademicScopePolicy.ResolveStageIds(profile.RoleName, profile.Email, stages))
			{
				if (!existingKeys.Add((employee.Id, stageId)))
				{
					continue;
				}

				assignments.Add(new EmployeeOrganizationAssignment(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					employee.Id,
					stageId));
			}
		}

		if (assignments.Count > 0)
		{
			await employeeOrganizationAssignmentRepository.InsertManyAsync(assignments, autoSave: false);
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		existing.AddRange(assignments);
		return new EmployeeOrganizationAssignmentBackfillResult(
			existing,
			assignments.Count,
			requiredBoardByEmployeeId,
			boardByStageId);
	}

	private sealed record EmployeeOrganizationAssignmentBackfillResult(
		List<EmployeeOrganizationAssignment> Assignments,
		int InsertedCount,
		IReadOnlyDictionary<Guid, EducationBoardType> RequiredBoardByEmployeeId,
		IReadOnlyDictionary<Guid, EducationBoardType> BoardByStageId);
}
