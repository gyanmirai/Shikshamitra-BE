using ShikshaMitra.Groups;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

public class HrReportPolicyScopeService(
	HrReportEmployeeQueryService employeeQueryService,
	IGroupRepository groupRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<IQueryable<HrPolicyAssignment>> ApplyScopeAsync(
		IQueryable<HrPolicyAssignment> query,
		UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return query;
		}

		var visibleAssignmentIds = await ResolveVisibleAssignmentIdsAsync(query, scope);
		return query.Where(x => visibleAssignmentIds.Contains(x.Id));
	}

	public async Task<HashSet<Guid>?> ResolveVisibleAssignmentIdsForAuditAsync(
		IQueryable<HrPolicyAssignment> query,
		UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return null;
		}

		return (await ResolveVisibleAssignmentIdsAsync(query, scope)).ToHashSet();
	}

	private async Task<List<Guid>> ResolveVisibleAssignmentIdsAsync(
		IQueryable<HrPolicyAssignment> query,
		UserScopeContext scope)
	{
		var scopedEmployees = await asyncExecuter.ToListAsync(
			(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope))
				.Where(employee => !employee.EmployeeStatusType.HasValue || employee.EmployeeStatusType == EmployeeStatusType.Active));
		var scopedEmployeeIds = scopedEmployees.Select(employee => employee.Id).ToHashSet();
		if (scopedEmployeeIds.Count == 0)
		{
			return [];
		}

		var assignments = await asyncExecuter.ToListAsync(query);
		var visibleIds = new List<Guid>();
		foreach (var assignment in assignments)
		{
			if (assignment.TargetType == HrPolicyAssignmentTargetType.Employee &&
				Guid.TryParse(assignment.TargetKey, out var employeeId) &&
				scopedEmployeeIds.Contains(employeeId))
			{
				visibleIds.Add(assignment.Id);
				continue;
			}

			if (assignment.TargetType == HrPolicyAssignmentTargetType.Group &&
				await GroupContainsScopedEmployeeAsync(assignment.TargetKey, scopedEmployees, scopedEmployeeIds))
			{
				visibleIds.Add(assignment.Id);
			}
		}

		return visibleIds;
	}

	private async Task<bool> GroupContainsScopedEmployeeAsync(
		string targetKey,
		IReadOnlyList<Employee> scopedEmployees,
		IReadOnlySet<Guid> scopedEmployeeIds)
	{
		if (!Guid.TryParse(targetKey, out var groupId))
		{
			return false;
		}

		var group = await groupRepository.GetWithDetailsAsync(groupId);
		if (group == null)
		{
			return false;
		}

		return HrPolicyGroupMembership
			.ResolveEmployeeIds(group, scopedEmployees)
			.Any(scopedEmployeeIds.Contains);
	}
}
