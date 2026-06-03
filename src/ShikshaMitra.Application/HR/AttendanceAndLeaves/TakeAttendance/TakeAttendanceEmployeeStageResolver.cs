using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceEmployeeStageResolver(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	HrEmployeeScopeQueryService hrEmployeeScopeQueryService,
	TakeAttendanceAccessService accessService)
	: ApplicationService, ITransientDependency
{
	public async Task<Dictionary<Guid, Guid?>> ResolveEmployeeStagesAsync(
		List<Guid> employeeIds,
		UserScopeContext scope)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, Guid?>();
		}

		var assignmentStageLookup = await hrEmployeeScopeQueryService.BuildEmployeeStageLookupAsync(employeeIds);
		var accessibleStageIds = await accessService.ResolveAccessibleStageIdsAsync(scope);
		var legacyStageByEmployee = await BuildLegacyStageLookupAsync(employeeIds);
		var result = new Dictionary<Guid, Guid?>();

		foreach (var employeeId in employeeIds)
		{
			assignmentStageLookup.TryGetValue(employeeId, out var assignedStages);
			var assignedStage = TakeAttendanceAccessService.ResolveAssignedStageForScope(assignedStages ?? new List<Guid>(), scope, accessibleStageIds);
			result[employeeId] = assignedStage ?? legacyStageByEmployee.GetValueOrDefault(employeeId);
		}

		return result;
	}

	private async Task<Dictionary<Guid, Guid?>> BuildLegacyStageLookupAsync(List<Guid> employeeIds)
	{
		var attQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			attQuery
				.Where(a => employeeIds.Contains(a.EmployeeId) && a.EducationMediumStageId.HasValue)
				.OrderByDescending(a => a.AttendanceDate)
				.Select(a => new { a.EmployeeId, a.EducationMediumStageId }));

		var lookup = new Dictionary<Guid, Guid?>();
		foreach (var row in rows)
		{
			if (!lookup.ContainsKey(row.EmployeeId))
			{
				lookup[row.EmployeeId] = row.EducationMediumStageId;
			}
		}

		return lookup;
	}
}
