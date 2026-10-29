using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipIssuedReader(ISalarySlipRepository slips, IRepository<PayslipDocument, Guid> documents,
	IEmployeeRepository employees, SalarySlipAccessService access, ICurrentTenant tenant) : ITransientDependency
{
	public async Task EnsureEmployeeAsync(Guid employeeId)
	{
		if (employeeId == Guid.Empty) throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
		await access.EnsureEmployeeScopeAllowedAsync(employeeId);
		var employee = await employees.GetAsync(employeeId, includeDetails: false);
		if (employee.TenantId != tenant.Id) throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	public async Task<SalarySlip> GetAsync(Guid employeeId, string monthYear)
	{
		await EnsureEmployeeAsync(employeeId);
		var slip = await slips.GetByEmployeeAndMonthAsync(employeeId, SalarySlipMonthYearNormalizer.Normalize(monthYear));
		return await EnsureIssuedAsync(slip);
	}

	public async Task<SalarySlip> GetAsync(Guid id)
	{
		var slip = await slips.GetAsync(id);
		await EnsureEmployeeAsync(slip.EmployeeId);
		return await EnsureIssuedAsync(slip);
	}

	private async Task<SalarySlip> EnsureIssuedAsync(SalarySlip? slip)
	{
		if (slip == null || slip.TenantId != tenant.Id ||
			slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
			throw new BusinessException(HRErrorCodes.PayslipNotAvailable);
		if (!await documents.AnyAsync(x => x.TenantId == tenant.Id && x.SalarySlipId == slip.Id &&
			x.EmployeeId == slip.EmployeeId && x.MonthYear == slip.MonthYear))
			throw new BusinessException(HRErrorCodes.PayslipDocumentRequired);
		return slip;
	}
}
