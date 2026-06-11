using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
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
			"[EmployeeAcademicPermissionSeeder] Synced stage permissions from employee organization assignments. LinkedEmployees={EmployeeCount}, Assignments={AssignmentCount}, AssignmentsInserted={AssignmentsInserted}, PermissionsInserted={InsertedCount}",
			employees.Count,
			assignments.Count,
			assignmentResult.InsertedCount,
			permissionsToInsert.Count);
	}

	private async Task<EmployeeOrganizationAssignmentBackfillResult> EnsureEmployeeOrganizationAssignmentsAsync(IReadOnlyCollection<Employee> employees)
	{
		var profilesByEmail = HRSeederContributor.BuildSuratSeedEmployeeProfiles();
		var stages = (await educationMediumStageRepository.GetQueryableAsync())
			.OrderBy(x => x.Name)
			.Select(x => new SeedStageInfo(x.Id, x.Name))
			.ToList();
		if (stages.Count == 0)
		{
			return new EmployeeOrganizationAssignmentBackfillResult([], 0);
		}

		var seededEmployees = employees
			.Where(x => x.Email != null && profilesByEmail.ContainsKey(x.Email))
			.ToList();
		if (seededEmployees.Count == 0)
		{
			return new EmployeeOrganizationAssignmentBackfillResult([], 0);
		}

		var employeeIds = seededEmployees.Select(x => x.Id).ToHashSet();
		var existing = await employeeOrganizationAssignmentRepository.GetListAsync(x =>
			employeeIds.Contains(x.EmployeeId));
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

			foreach (var stageId in ResolveSeedOrganizationIdsForEmployee(profile.RoleName, profile.Email, stages))
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
		return new EmployeeOrganizationAssignmentBackfillResult(existing, assignments.Count);
	}

	private static IReadOnlyCollection<Guid> ResolveSeedOrganizationIdsForEmployee(
		string roleName,
		string email,
		IReadOnlyList<SeedStageInfo> stages)
	{
		var broadAccessRoles = new[] { "Director", "Principal", "Admin", "Administrative", "Manager", "Clerk", "Finance", "HR" };
		if (broadAccessRoles.Any(token => roleName.Contains(token, StringComparison.OrdinalIgnoreCase)))
		{
			return stages.Select(x => x.Id).ToList();
		}

		var primary = stages.FirstOrDefault(x => x.Name.Contains("Primary", StringComparison.OrdinalIgnoreCase));
		var secondary = stages.FirstOrDefault(x => x.Name.Contains("Secondary", StringComparison.OrdinalIgnoreCase));
		if (roleName.Contains("Teacher", StringComparison.OrdinalIgnoreCase))
		{
			var preferred = StableSeedHash(email) % 2 == 0 ? primary ?? stages[0] : secondary ?? stages[^1];
			return new[] { preferred.Id };
		}

		return new[] { stages[0].Id };
	}

	private static uint StableSeedHash(string value)
	{
		unchecked
		{
			var hash = 2166136261u;
			foreach (var character in value.ToUpperInvariant())
			{
				hash ^= character;
				hash *= 16777619u;
			}

			return hash;
		}
	}

	private sealed record SeedStageInfo(Guid Id, string Name);

	private sealed record EmployeeOrganizationAssignmentBackfillResult(
		List<EmployeeOrganizationAssignment> Assignments,
		int InsertedCount);
}
