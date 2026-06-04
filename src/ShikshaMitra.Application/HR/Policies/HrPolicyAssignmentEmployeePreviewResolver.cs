using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.HR.Policies.HrPolicyAssignmentProjection;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentEmployeePreviewResolver(
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<HrPolicyAffectedEmployeeDto>> ResolveAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (targetType == HrPolicyAssignmentTargetType.Group)
		{
			return await ResolveGroupEmployeesAsync(employeeQuery, targetKey);
		}

		var query = targetType switch
		{
			HrPolicyAssignmentTargetType.Employee when Guid.TryParse(targetKey, out var employeeId) => employeeQuery.Where(x => x.Id == employeeId),
			_ => employeeQuery.Where(_ => false)
		};

		var employees = await asyncExecuter.ToListAsync(query.Take(25));
		return employees.Select(MapAffectedEmployee).ToList();
	}

	private async Task<List<HrPolicyAffectedEmployeeDto>> ResolveGroupEmployeesAsync(
		IQueryable<Employee> employeeQuery,
		string targetKey)
	{
		if (!Guid.TryParse(targetKey, out var groupId))
		{
			return [];
		}

		var group = await groupRepository.GetWithDetailsAsync(groupId);
		if (group == null)
		{
			return [];
		}

		var employeesForGroup = await asyncExecuter.ToListAsync(employeeQuery);
		var employeeIds = HrPolicyGroupMembership.ResolveEmployeeIds(group, employeesForGroup);
		var affected = employeesForGroup
			.Where(x => employeeIds.Contains(x.Id))
			.Take(25)
			.ToList();

		return affected.Select(MapAffectedEmployee).ToList();
	}
}
