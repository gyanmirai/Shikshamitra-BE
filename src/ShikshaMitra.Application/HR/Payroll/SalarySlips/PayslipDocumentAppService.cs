using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class PayslipDocumentAppService(IRepository<PayslipDocument, Guid> documents,
	ISalarySlipRepository slips, SalarySlipAccessService access, IPayrollInputRevisionStore fence,
	PayslipIssuanceService issuance, IEmployeeRepository employees,
	IRepository<PayslipCorrection, Guid> corrections) : ApplicationService, IPayslipDocumentAppService
{
	public async Task<List<PayslipDocumentDto>> GetAsync(Guid employeeId, string monthYear)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		await access.EnsureEmployeeScopeAllowedAsync(employeeId);
		var month = SalarySlipMonthYearNormalizer.Normalize(monthYear);
		var query = await documents.GetQueryableAsync();
		// Project metadata so history never loads all PDF artifacts.
		var records = await AsyncExecuter.ToListAsync(query.Where(x => x.EmployeeId == employeeId && x.MonthYear == month)
			.OrderByDescending(x => x.Revision).Select(x => new PayslipDocumentDto(x.Id, x.SalarySlipId,
				x.Revision, x.IssuedAt, x.IssuedBy, x.Sha256, false, x.ReplacesDocumentId, x.ReplacementReason)));
		if (records.Count == 0) return records;
		var employee = await employees.GetAsync(employeeId, includeDetails: false);
		var slip = await slips.GetAsync(records[0].SalarySlipId);
		return records.Select((x, i) => x with { IsCurrent = i == 0, RecipientEmail = employee.Email,
			PaymentStatus = slip.Status.ToString(), PaidAt = slip.PaidAt, PaymentReference = slip.PaymentReference }).ToList();
	}

	public async Task<byte[]> GetPdfAsync(Guid documentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var document = await documents.GetAsync(documentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		return document.Pdf;
	}

	[UnitOfWork(true)]
	public virtual async Task RecordPaymentAsync(RecordPayslipPaymentDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		await fence.LockAsync();
		var document = await documents.GetAsync(input.DocumentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		var slip = await slips.GetAsync(document.SalarySlipId);
		if (slip.Status == SalarySlipStatus.Finalized) await EnsureNoPendingCorrectionAsync(slip.Id);
		if ((await issuance.GetCurrentAsync(slip.Id)).Id != document.Id || input.PaidAt == default || input.PaidAt.UtcDateTime > Clock.Now)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		slip.RecordPayment(input.PaidAt, input.Reference, CurrentUser.Id ?? Guid.Empty, input.IdempotencyKey);
		await slips.UpdateAsync(slip, autoSave: true);
	}

	[UnitOfWork(true)]
	public virtual async Task<PayslipDocumentDto> ReplaceFactsAsync(ReplacePayslipFactsDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		await fence.LockAsync();
		var document = await documents.GetAsync(input.DocumentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		var slip = await slips.GetAsync(document.SalarySlipId);
		await EnsureNoPendingCorrectionAsync(slip.Id);
		var replacement = await issuance.ReplaceFactsAsync(slip, document, input.IdempotencyKey, input.Reason);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return new PayslipDocumentDto(replacement.Id, slip.Id, replacement.Revision, replacement.IssuedAt,
			replacement.IssuedBy, replacement.Sha256, true, replacement.ReplacesDocumentId, replacement.ReplacementReason);
	}

	private async Task EnsureNoPendingCorrectionAsync(Guid slipId)
	{
		if (await corrections.AnyAsync(x => x.SalarySlipId == slipId &&
			(x.Status == "DRAFT" || x.Status == "CALCULATED" || x.Status == "REVIEWED")))
			throw new BusinessException(HRErrorCodes.PayslipCorrectionPending);
	}
}
