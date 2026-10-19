using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAffectedEmployeeResolver(
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	internal async Task<AffectedEmployeePreview> ResolveAsync(Guid academicYearId, IReadOnlyList<HrPolicyAssignment> assignments)
	{
		if (assignments.Count == 0)
		{
			return AffectedEmployeePreview.Empty;
		}

		var directEmployeeIds = assignments
			.Where(assignment => assignment.TargetType == HrPolicyAssignmentTargetType.Employee && Guid.TryParse(assignment.TargetKey, out _))
			.Select(assignment => Guid.Parse(assignment.TargetKey))
			.ToHashSet();
		var groupIds = assignments
			.Where(assignment => assignment.TargetType == HrPolicyAssignmentTargetType.Group && Guid.TryParse(assignment.TargetKey, out _))
			.Select(assignment => Guid.Parse(assignment.TargetKey))
			.ToHashSet();

		var groups = groupIds.Count == 0
			? new Dictionary<Guid, Group>()
			: (await groupRepository.GetListWithDetailsAsync(academicYearId))
				.Where(group => groupIds.Contains(group.Id))
				.ToDictionary(group => group.Id);
		var groupsHaveRuleBasedMembership = groups.Values.Any(HasRuleBasedEmployeeMembership);
		var employeeQuery = ActiveEmployeeQuery(await employeeRepository.GetQueryableAsync());
		if (await asyncExecuter.CountAsync(employeeQuery) > 500000) throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);

		if (groups.Count == 0)
		{
			return await BuildEmployeePreviewFromIdsAsync(employeeQuery, directEmployeeIds);
		}

		if (!groupsHaveRuleBasedMembership)
		{
			var employeeIds = groups.Values
				.SelectMany(group => group.Members)
				.Where(member => member.MemberType == GroupMemberType.Employee)
				.Select(member => member.MemberId)
				.Concat(directEmployeeIds)
				.ToHashSet();
			return await BuildEmployeePreviewFromIdsAsync(employeeQuery, employeeIds);
		}

		var employees = await asyncExecuter.ToListAsync(employeeQuery);
		var matchedEmployees = employees
			.Where(employee =>
				directEmployeeIds.Contains(employee.Id) ||
				assignments.Any(assignment => Matches(employee, assignment, groups)))
			.GroupBy(employee => employee.Id)
			.Select(group => group.First())
			.ToList();

		return new AffectedEmployeePreview(
			matchedEmployees.Count,
			matchedEmployees,
			matchedEmployees.Select(x => x.Id).ToList());
	}

	private async Task<AffectedEmployeePreview> BuildEmployeePreviewFromIdsAsync(
		IQueryable<Employee> employeeQuery,
		IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return AffectedEmployeePreview.Empty;
		}

		var query = employeeQuery.Where(employee => employeeIds.Contains(employee.Id));
		var resolvedEmployeeIds = await asyncExecuter.ToListAsync(query.Select(employee => employee.Id));
		var sample = await asyncExecuter.ToListAsync(query.OrderBy(x => x.Id));
		return new AffectedEmployeePreview(resolvedEmployeeIds.Count, sample, resolvedEmployeeIds);
	}

	private static IQueryable<Employee> ActiveEmployeeQuery(IQueryable<Employee> query) =>
		query.Where(employee => !employee.EmployeeStatusType.HasValue || employee.EmployeeStatusType == EmployeeStatusType.Active);

	private static bool HasRuleBasedEmployeeMembership(Group group) =>
		group.RuleBasedRoleIds.Count > 0 || group.GetRuleBasedEmployeeRules().Any();

	private static bool Matches(Employee employee, HrPolicyAssignment assignment, IReadOnlyDictionary<Guid, Group> groups) => assignment.TargetType switch
	{
		HrPolicyAssignmentTargetType.Employee => employee.Id.ToString("D").Equals(assignment.TargetKey, StringComparison.OrdinalIgnoreCase),
		HrPolicyAssignmentTargetType.Group when Guid.TryParse(assignment.TargetKey, out var groupId) && groups.TryGetValue(groupId, out var group) =>
			HrPolicyGroupMembership.MatchesEmployee(group, employee),
		_ => false
	};
}
