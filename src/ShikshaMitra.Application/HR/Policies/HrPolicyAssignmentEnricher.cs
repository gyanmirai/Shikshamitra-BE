using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using static ShikshaMitra.HR.Policies.HrPolicyAssignmentProjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyAssignmentEnricher(
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver)
	: ApplicationService
{
	public async Task EnrichEffectiveTargetDisplayNamesAsync(EffectiveEmployeePoliciesDto dto)
	{
		var employeeIds = new HashSet<Guid>();
		var groupIds = new HashSet<Guid>();
		CollectTargetId(dto.MatchedTargetType, dto.MatchedTargetKey, employeeIds, groupIds);
		if (dto.Assignment != null)
		{
			CollectTargetId(dto.Assignment.TargetType, dto.Assignment.TargetKey, employeeIds, groupIds);
		}

		foreach (var step in dto.ResolutionSteps)
		{
			CollectTargetId(step.TargetType, step.TargetKey, employeeIds, groupIds);
		}

		var employeeNames = await ResolveEmployeeDisplayNamesAsync(employeeIds);
		var groupNames = await ResolveGroupDisplayNamesAsync(dto.AcademicYearId, groupIds);

		dto.MatchedTargetDisplayName = ResolveTargetDisplayName(
			dto.MatchedTargetType,
			dto.MatchedTargetKey,
			dto.MatchedTargetDisplayName,
			employeeNames,
			groupNames);

		if (dto.Assignment != null)
		{
			dto.Assignment.TargetDisplayName = ResolveTargetDisplayName(
				dto.Assignment.TargetType,
				dto.Assignment.TargetKey,
				dto.Assignment.TargetDisplayName,
				employeeNames,
				groupNames);
		}

		foreach (var step in dto.ResolutionSteps)
		{
			step.TargetDisplayName = ResolveTargetDisplayName(
				step.TargetType,
				step.TargetKey,
				step.TargetDisplayName,
				employeeNames,
				groupNames);
		}
	}

	public async Task EnrichTargetOrganizationsAsync(IReadOnlyCollection<HrPolicyAssignmentDto> assignments)
	{
		var employeeIds = assignments
			.Where(x => string.Equals(x.TargetType, "employee", StringComparison.OrdinalIgnoreCase))
			.Select(x => Guid.TryParse(x.TargetKey, out var employeeId) ? employeeId : (Guid?)null)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();

		if (employeeIds.Count == 0)
		{
			return;
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId));
		var organizationsByEmployeeId = employees.ToDictionary(
			x => x.Id,
			x => x.UserId.HasValue && organizationsByUserId.TryGetValue(x.UserId.Value, out var organizations)
				? organizations
					.Select(organization => organization.Name)
					.Where(name => !string.IsNullOrWhiteSpace(name))
					.Distinct()
					.OrderBy(name => name)
					.ToList()
				: []);

		foreach (var assignment in assignments)
		{
			if (!string.Equals(assignment.TargetType, "employee", StringComparison.OrdinalIgnoreCase) ||
			    !Guid.TryParse(assignment.TargetKey, out var employeeId) ||
			    !organizationsByEmployeeId.TryGetValue(employeeId, out var organizationNames))
			{
				continue;
			}

			assignment.TargetOrganizationNames = organizationNames;
		}
	}

	private async Task<Dictionary<string, string>> ResolveEmployeeDisplayNamesAsync(IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<string, string>();
		}

		var query = await employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(query.Where(x => employeeIds.Contains(x.Id)));
		return employees.ToDictionary(x => x.Id.ToString("D").ToLowerInvariant(), BuildEmployeeDisplayName);
	}

	private async Task<Dictionary<string, string>> ResolveGroupDisplayNamesAsync(Guid academicYearId, IReadOnlyCollection<Guid> groupIds)
	{
		if (groupIds.Count == 0)
		{
			return new Dictionary<string, string>();
		}

		var groups = await groupRepository.GetListWithDetailsAsync(academicYearId);
		return groups
			.Where(x => groupIds.Contains(x.Id))
			.ToDictionary(x => x.Id.ToString("D").ToLowerInvariant(), x => x.Name);
	}
}
