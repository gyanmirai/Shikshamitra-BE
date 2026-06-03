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
		var result = new Dictionary<Guid, Guid?>();

		foreach (var employeeId in employeeIds)
		{
			assignmentStageLookup.TryGetValue(employeeId, out var assignedStages);
			var assignedStage = TakeAttendanceAccessService.ResolveAssignedStageForScope(assignedStages ?? new List<Guid>(), scope, accessibleStageIds);
			result[employeeId] = assignedStage;
		}

		return result;
	}
}
