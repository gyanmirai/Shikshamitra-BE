using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollApprovalSettingsWorkflow(PayrollApprovalAccess access, IPayrollInputRevisionStore revisions,
	IRepository<PayrollApprovalSettings, Guid> settings, IUnitOfWorkManager units, HrAuditWriter audit)
	: ITransientDependency
{
	internal async Task<PayrollApprovalSettingsDto> GetAsync() => ToDto(await access.ReadSettingsAsync());

	internal Task<PayrollApprovalSettingsDto> ConfigureAsync(ConfigurePayrollApprovalCommand command) =>
		ChangeAsync("configure", command, async current =>
		{
			await EnsureApproverAsync(command.ApproverId);
			current.Configure(command.ApproverId, command.AllowSelfFinalization);
		});

	internal Task<PayrollApprovalSettingsDto> ChangeModeAsync(string action, PayrollApprovalSettingsCommand command) =>
		ChangeAsync(action, command, async current =>
		{
			if (action is "activate" or "resume") await EnsureApproverAsync(current.ApproverId);
			current.Transition(action);
		});

	private async Task<PayrollApprovalSettingsDto> ChangeAsync(string action, PayrollApprovalSettingsCommand command,
		Func<PayrollApprovalSettings, Task> change)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		await revisions.LockAsync();
		if (!await access.CanManageAsync()) throw new AbpAuthorizationException();
		if (string.IsNullOrWhiteSpace(command.Reason)) throw new BusinessException("PAYROLL_CHANGE_REASON_REQUIRED");
		var stored = await access.ReadSettingsAsync();
		var current = stored ?? new PayrollApprovalSettings(access.TenantId);
		if (current.Version != command.ExpectedVersion) throw new BusinessException("PAYROLL_SETTINGS_VERSION_CONFLICT");
		await change(current);
		if (stored == null) await settings.InsertAsync(current);
		else await settings.UpdateAsync(current);
		await audit.WriteAsync(null, "payroll_approval_settings", current.Id, "Payroll approval settings", action,
			"Updated payroll approval settings.", command.Reason.Trim(), context: ToDto(current));
		await unit.CompleteAsync();
		return ToDto(current);
	}

	private async Task EnsureApproverAsync(Guid? id)
	{
		if (id == null || !await access.CanApproveAsync(id.Value, false))
			throw new BusinessException("PAYROLL_APPROVER_PERMISSION_REQUIRED");
	}

	private static PayrollApprovalSettingsDto ToDto(PayrollApprovalSettings? setting) =>
		new(setting?.Mode ?? PayrollApprovalSettings.Legacy, setting?.Version ?? 0,
			setting?.ApproverId, setting?.AllowSelfFinalization ?? false);
}
