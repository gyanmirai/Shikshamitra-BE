using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeOrganizationAssignmentSyncer(
	IEmployeeRepository employeeRepository,
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver,
	EmployeeOrganizationScopeLookupReader organizationScopeLookupReader,
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task SyncFromAcademicPermissionsAsync(
		IReadOnlyCollection<Guid> userIds,
		IReadOnlyCollection<Guid> academicYearIds)
	{
		var targetUserIds = userIds
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToList();
		var targetAcademicYearIds = academicYearIds
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToHashSet();
		if (targetUserIds.Count == 0 || targetAcademicYearIds.Count == 0)
		{
			return;
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(employeeQuery
			.Where(x => x.UserId.HasValue && targetUserIds.Contains(x.UserId.Value)));
		if (employees.Count == 0)
		{
			return;
		}

		var selectedEmployeeByUserId = employees
			.Where(x => x.UserId.HasValue)
			.GroupBy(x => x.UserId!.Value)
			.ToDictionary(x => x.Key, ChooseEmployeeForUser);
		var employeeIds = selectedEmployeeByUserId.Values.Select(x => x.Id).Distinct().ToList();
		var requestedStageIdsByEmployeeId = employeeIds.ToDictionary(x => x, _ => new HashSet<Guid>());

		foreach (var academicYearId in targetAcademicYearIds)
		{
			var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
				selectedEmployeeByUserId.Keys.Select(x => (Guid?)x),
				academicYearId);

			foreach (var (userId, organizations) in organizationsByUserId)
			{
				if (!selectedEmployeeByUserId.TryGetValue(userId, out var employee))
				{
					continue;
				}

				var requestedStageIds = requestedStageIdsByEmployeeId[employee.Id];
				foreach (var organization in organizations)
				{
					requestedStageIds.Add(organization.Id);
				}
			}
		}

		var targetYearStageIds = (await organizationScopeLookupReader.LoadStageInfosAsync(null))
			.Where(x => targetAcademicYearIds.Contains(x.AcademicYearId))
			.Select(x => x.Id)
			.ToHashSet();
		var assignmentQuery = await employeeOrganizationAssignmentRepository.GetQueryableAsync();
		var existingAssignments = await asyncExecuter.ToListAsync(assignmentQuery
			.Where(x => employeeIds.Contains(x.EmployeeId) && targetYearStageIds.Contains(x.EducationMediumStageId)));
		var existingStageIdsByEmployeeId = existingAssignments
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(x => x.Key, x => x.Select(y => y.EducationMediumStageId).ToHashSet());

		foreach (var assignment in existingAssignments)
		{
			if (!requestedStageIdsByEmployeeId.TryGetValue(assignment.EmployeeId, out var requestedStageIds) ||
			    !requestedStageIds.Contains(assignment.EducationMediumStageId))
			{
				await employeeOrganizationAssignmentRepository.DeleteAsync(assignment, autoSave: false);
			}
		}

		foreach (var (employeeId, requestedStageIds) in requestedStageIdsByEmployeeId)
		{
			existingStageIdsByEmployeeId.TryGetValue(employeeId, out var existingStageIds);
			existingStageIds ??= [];

			foreach (var stageId in requestedStageIds.Where(x => targetYearStageIds.Contains(x)).Except(existingStageIds))
			{
				await employeeOrganizationAssignmentRepository.InsertAsync(
					new EmployeeOrganizationAssignment(
						guidGenerator.Create(),
						currentTenant.Id,
						employeeId,
						stageId),
					autoSave: false);
			}
		}
	}

	private static Employee ChooseEmployeeForUser(IEnumerable<Employee> employees) =>
		employees
			.OrderByDescending(x => x.EmployeeStatusType == EmployeeStatusType.Active)
			.ThenByDescending(x => x.LastModificationTime ?? x.CreationTime)
			.First();
}
