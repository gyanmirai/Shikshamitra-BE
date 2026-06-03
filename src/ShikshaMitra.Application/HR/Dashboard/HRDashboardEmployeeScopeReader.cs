using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardEmployeeScopeReader(
	IEmployeeRepository employeeRepository,
	IEmployeeAttendanceRepository attendanceRepository,
	HrUserScopeAccessService hrScopeAccessService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(UserScopeContext scope)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeQuery;
		}

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();

		var employeeStageRows = await asyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a => a.EducationMediumStageId.HasValue)
				.Select(a => new { a.EmployeeId, a.EducationMediumStageId }));
		var employeeStageMap = employeeStageRows
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<Guid>)x
					.Select(row => row.EducationMediumStageId!.Value)
					.Distinct()
					.ToList());
		var scopedStageEmployeeIds = await hrScopeAccessService.FilterEmployeeIdsByStagesAsync(
			employeeStageMap.Keys.ToList(),
			employeeStageMap,
			scope,
			includeUnknownStageEmployees: false);

		return employeeQuery.Where(e => scopedStageEmployeeIds.Contains(e.Id));
	}
}
