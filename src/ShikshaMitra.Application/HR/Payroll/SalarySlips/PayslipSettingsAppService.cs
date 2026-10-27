using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Microsoft.AspNetCore.Authorization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class PayslipSettingsAppService(IRepository<PayslipSettings, Guid> settings, IPayrollInputRevisionStore fence,
	SalarySlipAccessService access, PayslipLogoService logos, PayrollDocumentSchoolService school, PayslipPdfService pdf)
	: ApplicationService, IPayslipSettingsAppService
{
	public Task<string> UploadLogoAsync(string contentType, byte[] bytes) => logos.UploadAsync(contentType, bytes);
	// Read-only: no employee lookup, settings write, issuance or delivery side effects.
	public async Task<byte[]> PreviewAsync(SalaryDocumentPresentation input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var tenant = CurrentTenant.Id ?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		if (input == null) throw new BusinessException("HR:Payslip:PresentationInvalid");
		input = input with { LogoBlobName = null };
		input.Validate(tenant);
		var details = await school.ReadAsync();
		return pdf.Generate(PayslipSettingsPreview.Create(details.Name, details.Address, input) with { LogoBytes = await school.LogoAsync() });
	}
	public async Task<PayslipSettingsDto> GetAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var tenant = CurrentTenant.Id ?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		var existing = await settings.FindAsync(tenant);
		return await ToDtoAsync(existing);
	}

	[UnitOfWork(true)]
	public virtual async Task<PayslipSettingsDto> SaveAsync(PayslipSettingsDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		if (input.IdempotencyKey == Guid.Empty)
			throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		await access.EnsureHrWideAccessAsync();
		var tenant = CurrentTenant.Id ?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		var details = await school.ReadAsync();
		if (input.Presentation == null) throw new BusinessException("HR:Payslip:PresentationInvalid");
		input.Presentation = input.Presentation with { LogoBlobName = null };
		input.Presentation.Validate(tenant);
		await fence.LockAsync();
		var existing = await settings.FindAsync(tenant);
		if (existing != null && existing.LastRequestId == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
		{
			if (existing.AutomaticEmail != input.AutomaticEmail || System.Text.Json.JsonSerializer.Serialize(existing.GetPresentation()) != System.Text.Json.JsonSerializer.Serialize(input.Presentation)) throw new BusinessException(HRErrorCodes.PayslipConflict);
			return await ToDtoAsync(existing);
		}
		if (existing?.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException(HRErrorCodes.PayslipConflict);
		var record = existing ?? new PayslipSettings(tenant);
		record.Configure(details.Name, details.Address, input.AutomaticEmail, input.IdempotencyKey, input.Presentation);
		if (existing == null) await settings.InsertAsync(record, autoSave: true);
		else await settings.UpdateAsync(record, autoSave: true);
		return await ToDtoAsync(record);
	}

	private async Task<PayslipSettingsDto> ToDtoAsync(PayslipSettings? settings)
	{
		var details = await school.ReadAsync();
		var presentation = settings?.GetPresentation() ?? new();
		presentation = presentation with { LogoBlobName = null };
		return new()
		{
			Presentation = presentation, PresentationVersionId = settings?.PresentationVersionId ?? Guid.Empty,
			SchoolName = details.Name, SchoolAddress = details.Address,
			AutomaticEmail = settings?.AutomaticEmail ?? false, ConcurrencyStamp = settings?.ConcurrencyStamp,
			IdempotencyKey = settings?.LastRequestId ?? Guid.Empty
		};
	}
}
