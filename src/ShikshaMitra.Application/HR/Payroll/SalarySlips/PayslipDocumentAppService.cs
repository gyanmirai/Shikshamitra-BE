using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
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
	public async Task<PagedResultDto<IssuedPayslipListItem>> GetIssuedAsync(IssuedPayslipListInput input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var staff = await access.GetIssuedHistoryEmployeesAsync();
		var tenantId = CurrentTenant.Id;
		var issued = (await documents.GetQueryableAsync()).Where(document => document.TenantId == tenantId);
		var finalized = (await slips.GetQueryableAsync()).Where(slip => slip.TenantId == tenantId &&
			(slip.Status == SalarySlipStatus.Finalized || slip.Status == SalarySlipStatus.Paid));
		if (!string.IsNullOrWhiteSpace(input.MonthYear))
		{
			var month = SalarySlipMonthYearNormalizer.Normalize(input.MonthYear);
			issued = issued.Where(document => document.MonthYear == month);
		}
		var search = input.Search?.Trim().ToLowerInvariant();
		if (!string.IsNullOrEmpty(search))
			staff = staff.Where(employee =>
				((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).ToLower().Contains(search) ||
				(employee.EmployeeCode != null && employee.EmployeeCode.ToLower().Contains(search)));

		var query =
			from document in issued
			join slip in finalized on document.SalarySlipId equals slip.Id
			join employee in staff on document.EmployeeId equals employee.Id
			where document.EmployeeId == slip.EmployeeId && document.MonthYear == slip.MonthYear &&
				!issued.Any(newer => newer.SalarySlipId == document.SalarySlipId && newer.Revision > document.Revision)
			select new { document, slip.Status, employee.FirstName, employee.LastName, employee.EmployeeCode };
		var total = await AsyncExecuter.LongCountAsync(query);
		if (input.SkipCount >= total)
			return new PagedResultDto<IssuedPayslipListItem>(total, []);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.document.MonthYear)
			.ThenBy(row => row.FirstName).ThenBy(row => row.LastName).ThenBy(row => row.document.Id)
			.Skip(input.SkipCount).Take(input.MaxResultCount)
			.Select(row => new IssuedPayslipListItem(row.document.Id, row.document.EmployeeId,
				((row.FirstName ?? "") + " " + (row.LastName ?? "")).Trim(), row.EmployeeCode,
				row.document.MonthYear, row.document.Revision, row.document.IssuedAt, row.Status == SalarySlipStatus.Paid)));
		return new PagedResultDto<IssuedPayslipListItem>(total, rows);
	}

	public async Task<List<PayslipDocumentDto>> GetAsync(Guid employeeId, string monthYear)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		await access.EnsureEmployeeScopeAllowedAsync(employeeId);
		var month = SalarySlipMonthYearNormalizer.Normalize(monthYear);
		var slip = await slips.GetByEmployeeAndMonthAsync(employeeId, month);
		if (slip == null || slip.TenantId != CurrentTenant.Id ||
			slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid)) return [];
		var query = await documents.GetQueryableAsync();
		// Project metadata so history never loads all PDF artifacts.
		var records = await AsyncExecuter.ToListAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			x.SalarySlipId == slip.Id && x.EmployeeId == employeeId && x.MonthYear == month)
			.OrderByDescending(x => x.Revision).Select(x => new PayslipDocumentDto(x.Id, x.SalarySlipId,
				x.Revision, x.IssuedAt, x.IssuedBy, x.Sha256, false, x.ReplacesDocumentId, x.ReplacementReason)));
		if (records.Count == 0) return records;
		var employee = await employees.GetAsync(employeeId, includeDetails: false);
		return records.Select((x, i) => x with
		{
			IsCurrent = i == 0,
			RecipientEmail = employee.Email,
			PaymentStatus = slip.Status.ToString(),
			PaidAt = slip.PaidAt,
			PaymentReference = slip.PaymentReference
		}).ToList();
	}

	public async Task<byte[]> GetPdfAsync(Guid documentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var document = await documents.GetAsync(documentId);
		await access.EnsureEmployeeScopeAllowedAsync(document.EmployeeId);
		var slip = await slips.GetAsync(document.SalarySlipId);
		if (document.TenantId != CurrentTenant.Id || slip.TenantId != CurrentTenant.Id ||
			slip.EmployeeId != document.EmployeeId || slip.MonthYear != document.MonthYear ||
			slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
			throw new BusinessException(HRErrorCodes.PayslipNotAvailable);
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
		if ((await issuance.GetCurrentAsync(slip.Id)).Id != document.Id || input.PaidAt == default || input.PaidAt.UtcDateTime > Clock.Now.ToUniversalTime())
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
