using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<List<Guid>> SeedEmployeesAsync()
	{
		var seededEmployeeIds = await TimeHrPhaseAsync(
			"Phase 4a employee batch create/update",
			SeedEmployeesFromProfilesAsync);

		await TimeHrPhaseAsync(
			"Phase 4b reporting chain",
			EnsureSeededReportingChainAsync);

		return seededEmployeeIds;
	}

	private async Task<List<Guid>> SeedEmployeesFromProfilesAsync()
	{
		var profiles = BuildSuratSeedEmployeeProfiles().Values.ToList();
		var profileEmails = profiles.Select(p => p.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var fullDayShiftId = ResolveSeedDefaultShiftId(await _shiftRepository.GetListAsync());
		var existingEmployees = (await _employeeRepository.GetListAsync())
			.Where(x => x.Email != null && profileEmails.Contains(x.Email))
			.ToList();
		var employeesByEmail = existingEmployees
			.Where(x => !string.IsNullOrWhiteSpace(x.Email))
			.ToDictionary(x => x.Email!, StringComparer.OrdinalIgnoreCase);
		var rolesByName = await GetSeedRolesByNameAsync();
		var employeeCodesByEmail = await GenerateSeedEmployeeCodesAsync(profiles, rolesByName, existingEmployees);
		var employeesToInsert = new List<Employee>();
		var employeesToUpdate = new List<Employee>();

		ValidateSeedProfiles(profiles, rolesByName);

		foreach (var profile in profiles)
		{
			var userId = await CreateUserForEmployeeAsync(
				profile.FirstName,
				profile.LastName,
				profile.Email,
				profile.Mobile,
				profile.RoleName);

			var role = rolesByName[NormalizeRoleName(profile.RoleName)];
			if (employeesByEmail.TryGetValue(profile.Email, out var existingEmployee))
			{
				UpdateSeedEmployee(existingEmployee, profile, role.Id, fullDayShiftId, userId);
				employeesToUpdate.Add(existingEmployee);
				continue;
			}

			var employee = CreateSeedEmployee(
				profile,
				userId,
				role.Id,
				employeeCodesByEmail[profile.Email],
				fullDayShiftId);
			employeesToInsert.Add(employee);
			employeesByEmail[profile.Email] = employee;
		}

		if (employeesToInsert.Count > 0)
		{
			await _employeeRepository.InsertManyAsync(employeesToInsert, autoSave: false);
		}

		if (employeesToUpdate.Count > 0)
		{
			await _employeeRepository.UpdateManyAsync(employeesToUpdate, autoSave: false);
		}

		if (employeesToInsert.Count > 0 || employeesToUpdate.Count > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation(
			"[HRSeeder] Seed employee batch ensured created={CreatedCount}, updated={UpdatedCount}",
			employeesToInsert.Count,
			employeesToUpdate.Count);

		return employeesToInsert.Select(x => x.Id).Concat(employeesToUpdate.Select(x => x.Id)).Distinct().ToList();
	}

	private async Task EnsureEmployeesHaveOrganizationAssignmentsAsync()
	{
		var stageIds = (await _educationMediumStageRepository.GetQueryableAsync())
			.Select(x => x.Id)
			.ToList();
		if (stageIds.Count == 0)
		{
			return;
		}

		var employees = (await _employeeRepository.GetQueryableAsync()).ToList();
		var existingAssignments = await _employeeOrganizationAssignmentRepository.GetListAsync();
		var assignedEmployeeIds = existingAssignments
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var assignmentsToInsert = new List<EmployeeOrganizationAssignment>();

		foreach (var employee in employees.Where(x => !assignedEmployeeIds.Contains(x.Id)))
		{
			assignmentsToInsert.AddRange(stageIds.Select(stageId => new EmployeeOrganizationAssignment(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employee.Id,
				stageId)));
		}

		if (assignmentsToInsert.Count > 0)
		{
			await _employeeOrganizationAssignmentRepository.InsertManyAsync(assignmentsToInsert, autoSave: true);
		}
	}

	private async Task EnsureEmployeeOrganizationAssignmentsAsync(Guid employeeId, IReadOnlyCollection<Guid> organizationIds)
	{
		var existing = await _employeeOrganizationAssignmentRepository.GetListAsync(x => x.EmployeeId == employeeId);
		var existingIds = existing.Select(x => x.EducationMediumStageId).ToHashSet();
		var assignments = organizationIds
			.Where(id => !existingIds.Contains(id))
			.Distinct()
			.Select(id => new EmployeeOrganizationAssignment(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employeeId,
				id))
			.ToList();

		if (assignments.Count > 0)
		{
			await _employeeOrganizationAssignmentRepository.InsertManyAsync(assignments, autoSave: true);
		}
	}

	private async Task<IReadOnlyCollection<Guid>> ResolveSeedOrganizationIdsForEmployeeAsync(string roleName, string email)
	{
		var stages = (await _educationMediumStageRepository.GetQueryableAsync())
			.OrderBy(x => x.Name)
			.Select(x => new { x.Id, x.Name })
			.ToList();
		if (stages.Count == 0)
		{
			return Array.Empty<Guid>();
		}

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
}
