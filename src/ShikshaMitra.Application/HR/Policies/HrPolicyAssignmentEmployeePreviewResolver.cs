using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
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
	internal async Task<AffectedEmployeePreview> ResolveAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey)
	{
		var employeeQuery = ActiveEmployeeQuery(await employeeRepository.GetQueryableAsync());
		if (targetType == HrPolicyAssignmentTargetType.Group)
		{
			return await ResolveGroupEmployeesAsync(employeeQuery, targetKey);
		}

		var query = targetType switch
		{
			HrPolicyAssignmentTargetType.Employee when Guid.TryParse(targetKey, out var employeeId) => employeeQuery.Where(x => x.Id == employeeId),
			_ => employeeQuery.Where(_ => false)
		};

		var count = await asyncExecuter.CountAsync(query);
		var employees = await asyncExecuter.ToListAsync(query.Take(25));
		return new AffectedEmployeePreview(count, employees);
	}

	private async Task<AffectedEmployeePreview> ResolveGroupEmployeesAsync(
		IQueryable<Employee> employeeQuery,
		string targetKey)
	{
		if (!Guid.TryParse(targetKey, out var groupId))
		{
			return AffectedEmployeePreview.Empty;
		}

		var group = await groupRepository.GetWithDetailsAsync(groupId);
		if (group == null)
		{
			return AffectedEmployeePreview.Empty;
		}

		var directEmployeeIds = group.Members
			.Where(member => member.MemberType == GroupMemberType.Employee)
			.Select(member => member.MemberId)
			.ToHashSet();
		var rules = group.GetRuleBasedEmployeeRules().ToList();
		if (group.RuleBasedRoleIds.Count == 0 && rules.Count == 0)
		{
			return await BuildEmployeePreviewFromQueryAsync(employeeQuery.Where(employee => directEmployeeIds.Contains(employee.Id)));
		}

		if (group.RuleBasedRoleIds.Count > 0 && rules.Count == 0)
		{
			var roleQuery = employeeQuery.Where(employee => group.RuleBasedRoleIds.Contains(employee.RoleId));
			if (directEmployeeIds.Count > 0)
			{
				roleQuery = employeeQuery.Where(employee =>
					directEmployeeIds.Contains(employee.Id) ||
					group.RuleBasedRoleIds.Contains(employee.RoleId));
			}

			return await BuildEmployeePreviewFromQueryAsync(roleQuery);
		}

		var employeesForGroupQuery = employeeQuery;
		if (group.RuleBasedRoleIds.Count > 0)
		{
			employeesForGroupQuery = directEmployeeIds.Count == 0
				? employeesForGroupQuery.Where(employee => group.RuleBasedRoleIds.Contains(employee.RoleId))
				: employeesForGroupQuery.Where(employee =>
					directEmployeeIds.Contains(employee.Id) ||
					group.RuleBasedRoleIds.Contains(employee.RoleId));
		}

		var employeesForGroup = await asyncExecuter.ToListAsync(employeesForGroupQuery);
		var employeeIds = HrPolicyGroupMembership.ResolveEmployeeIds(group, employeesForGroup);
		var affected = employeesForGroup
			.Where(x => employeeIds.Contains(x.Id))
			.ToList();

		return new AffectedEmployeePreview(affected.Count, affected.Take(25).ToList());
	}

	private async Task<AffectedEmployeePreview> BuildEmployeePreviewFromQueryAsync(IQueryable<Employee> query)
	{
		var count = await asyncExecuter.CountAsync(query);
		var employees = await asyncExecuter.ToListAsync(query.Take(25));
		return new AffectedEmployeePreview(count, employees);
	}

	private static IQueryable<Employee> ActiveEmployeeQuery(IQueryable<Employee> query) =>
		query.Where(employee => !employee.EmployeeStatusType.HasValue || employee.EmployeeStatusType == EmployeeStatusType.Active);
}
