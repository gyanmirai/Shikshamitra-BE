using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipAccessService(
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HrEmployeeScopeQueryService employeeScopeQueryService)
	: ApplicationService
{
	public async Task EnsureEmployeeScopeAllowedAsync(Guid employeeId)
	{
		var scope = await EnsureCanOperatePayrollAsync();
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return;
		}

		var stageLookup = await employeeScopeQueryService.BuildEmployeeStageLookupAsync([employeeId]);
		if (!stageLookup.TryGetValue(employeeId, out var stageIds) || stageIds.Count == 0)
		{
			hrScopeAccessService.LogDeniedAccess(scope, nameof(EnsureEmployeeScopeAllowedAsync), "Employee", employeeId);
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		var accessibleEmployeeIds = await hrScopeAccessService.FilterEmployeeIdsByStagesAsync(
			[employeeId],
			stageLookup.ToDictionary(x => x.Key, x => (IReadOnlyCollection<Guid>)x.Value),
			scope,
			includeUnknownStageEmployees: false);
		if (accessibleEmployeeIds.Count == 0)
		{
			hrScopeAccessService.LogDeniedAccess(scope, nameof(EnsureEmployeeScopeAllowedAsync), "Employee", employeeId);
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}
	}

	private async Task<UserScopeContext> EnsureCanOperatePayrollAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		hrScopeAccessService.EnsureHasConcreteHrAccess(scope, nameof(EnsureCanOperatePayrollAsync));

		return scope;
	}
}
