using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipPdfWorkflow(
	ISalarySlipRepository salarySlipRepository,
	IEmployeeRepository employeeRepository,
	SalarySlipDtoMapper salarySlipDtoMapper,
	PayslipPdfService payslipPdfService,
	SalarySlipAccessService accessService)
	: ApplicationService
{
	public async Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear)
	{
		if (employeeId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
		}

		await accessService.EnsureEmployeeScopeAllowedAsync(employeeId);
		var normalizedMonthYear = SalarySlipMonthYearNormalizer.Normalize(monthYear);
		var salarySlip = await salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, normalizedMonthYear);
		if (salarySlip == null)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipNotFound);
		}

		if (salarySlip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
		{
			throw new BusinessException(HRErrorCodes.SalarySlipPdfRequiresFinalized);
		}

		var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);
		var pdfModel = SalarySlipProjection.ToPdfModel(dto, CurrentTenant.Name ?? "ShikshaMitra");

		return payslipPdfService.Generate(pdfModel);
	}
}
