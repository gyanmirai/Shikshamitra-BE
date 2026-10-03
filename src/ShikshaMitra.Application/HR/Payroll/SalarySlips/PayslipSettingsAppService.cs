using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Microsoft.AspNetCore.Authorization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class PayslipSettingsAppService(IRepository<PayslipSettings, Guid> settings, IPayrollInputRevisionStore fence,
	IUserScopeService scopes)
	: ApplicationService, IPayslipSettingsAppService
{
	public async Task<PayslipSettingsDto> GetAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var tenant = CurrentTenant.Id ?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		var existing = await settings.FindAsync(tenant);
		return existing == null ? new PayslipSettingsDto { SchoolName = CurrentTenant.Name ?? string.Empty } : ToDto(existing);
	}

	[UnitOfWork(true)]
	public virtual async Task<PayslipSettingsDto> SaveAsync(PayslipSettingsDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		if (string.IsNullOrWhiteSpace(input.SchoolName) || string.IsNullOrWhiteSpace(input.SchoolAddress) ||
			input.SchoolName.Trim().Length > 250 || input.SchoolAddress.Trim().Length > 1000 || input.IdempotencyKey == Guid.Empty)
			throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		if (!CurrentUser.Id.HasValue || !HrUserScopeAccess.HasHrWideAccess(await scopes.GetUserScopeAsync(CurrentUser.Id.Value)))
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		var tenant = CurrentTenant.Id ?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		await fence.LockAsync();
		var existing = await settings.FindAsync(tenant);
		if (existing != null && existing.LastRequestId == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
		{
			if (existing.SchoolName != input.SchoolName.Trim() || existing.SchoolAddress != input.SchoolAddress.Trim() ||
				existing.AutomaticEmail != input.AutomaticEmail) throw new BusinessException(HRErrorCodes.PayslipConflict);
			return ToDto(existing);
		}
		if (existing?.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException(HRErrorCodes.PayslipConflict);
		var record = existing ?? new PayslipSettings(tenant);
		record.Configure(input.SchoolName, input.SchoolAddress, input.AutomaticEmail, input.IdempotencyKey);
		if (existing == null) await settings.InsertAsync(record, autoSave: true);
		else await settings.UpdateAsync(record, autoSave: true);
		return ToDto(record);
	}

	private static PayslipSettingsDto ToDto(PayslipSettings settings) => new()
	{
		SchoolName = settings.SchoolName, SchoolAddress = settings.SchoolAddress,
		AutomaticEmail = settings.AutomaticEmail, ConcurrencyStamp = settings.ConcurrencyStamp,
		IdempotencyKey = settings.LastRequestId
	};
}
