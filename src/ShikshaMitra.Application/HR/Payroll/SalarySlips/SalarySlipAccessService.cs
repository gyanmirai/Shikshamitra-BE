using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipAccessService(
	IRepository<EmployeeAttendance, Guid> employeeAttendanceRepository,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService)
	: ApplicationService
{
	public async Task EnsureEmployeeScopeAllowedAsync(Guid employeeId)
	{
		var scope = await EnsureCanOperatePayrollAsync();
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return;
		}

		var employeeStageId = await ResolveEmployeeStageAsync(employeeId);
		await hrScopeAccessService.EnsureCanAccessEmployeeStageAsync(
			scope,
			employeeStageId,
			nameof(EnsureEmployeeScopeAllowedAsync),
			employeeId);
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

	private async Task<Guid?> ResolveEmployeeStageAsync(Guid employeeId)
	{
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();

		return await AsyncExecuter.FirstOrDefaultAsync(
			attendanceQuery
				.Where(a => a.EmployeeId == employeeId && a.EducationMediumStageId.HasValue)
				.OrderByDescending(a => a.AttendanceDate)
				.Select(a => a.EducationMediumStageId));
	}
}
