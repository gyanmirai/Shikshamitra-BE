using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportEmployeeQueryService(
	IEmployeeRepository employeeRepository,
	HrUserScopeAccessService hrScopeAccessService,
	EmployeeOrganizationScopeResolver organizationScopeResolver) : ITransientDependency
{
	public async Task<IQueryable<Employee>> BuildScopedEmployeeQueryAsync(UserScopeContext scope)
	{
		var query = await employeeRepository.GetQueryableAsync();

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return query;
		}

		hrScopeAccessService.EnsureHasConcreteHrAccess(scope, nameof(BuildScopedEmployeeQueryAsync));

		var accessibleStageIds = await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
		var matchingUserIds = await organizationScopeResolver.GetUserIdsWithAnyStageAsync(accessibleStageIds);
		return query.Where(e => e.UserId.HasValue && matchingUserIds.Contains(e.UserId.Value));
	}

	public static IQueryable<Employee> ApplyEmployeeFilters(IQueryable<Employee> query, HrReportFilterDto filters)
	{
		if (filters.EmployeeIds?.Count > 0)
		{
			query = query.Where(e => filters.EmployeeIds.Contains(e.Id));
		}

		if (filters.Departments?.Count > 0)
		{
			var departments = filters.Departments.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
			query = query.Where(e => e.Department != null && departments.Contains(e.Department));
		}

		if (filters.Statuses?.Count > 0)
		{
			var statuses = filters.Statuses.Select(x => (EmployeeStatusType)x).ToList();
			query = query.Where(e => e.EmployeeStatusType.HasValue && statuses.Contains(e.EmployeeStatusType.Value));
		}

		if (filters.GenderType.HasValue)
		{
			var gender = (GenderType)filters.GenderType.Value;
			query = query.Where(e => e.GenderType == gender);
		}

		return query;
	}
}
