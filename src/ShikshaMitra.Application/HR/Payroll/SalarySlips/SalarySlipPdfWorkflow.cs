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
	PayslipIssuanceService issuance,
	SalarySlipAccessService accessService,
	SalarySlipSnapshotGenerationService snapshotGenerationService)
	: ApplicationService
{
	public async Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear)
	{
		var salarySlip = await GetSalarySlipForPdfAsync(employeeId, monthYear, allowSnapshotPreview: false);

		if (salarySlip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
		{
			throw new BusinessException(HRErrorCodes.SalarySlipPdfRequiresFinalized);
		}

		return await GenerateAsync(salarySlip, isPreview: false);
	}

	public async Task<byte[]> GenerateSalarySlipPreviewPdfAsync(Guid employeeId, string monthYear)
	{
		var salarySlip = await GetSalarySlipForPdfAsync(employeeId, monthYear, allowSnapshotPreview: true);
		var isPreview = salarySlip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid);

		return await GenerateAsync(salarySlip, isPreview);
	}

	private async Task<SalarySlip> GetSalarySlipForPdfAsync(
		Guid employeeId,
		string monthYear,
		bool allowSnapshotPreview)
	{
		if (employeeId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.PayrollEmployeeIdRequired);
		}

		await accessService.EnsureEmployeeScopeAllowedAsync(employeeId);
		var normalizedMonthYear = SalarySlipMonthYearNormalizer.Normalize(monthYear);
		var salarySlip = await salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, normalizedMonthYear);
		if (allowSnapshotPreview && salarySlip?.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
		{
			return await snapshotGenerationService.GenerateOrGetFromSnapshotAsync(employeeId, normalizedMonthYear);
		}

		if (salarySlip == null)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipNotFound);
		}

		return salarySlip;
	}

	private async Task<byte[]> GenerateAsync(SalarySlip salarySlip, bool isPreview)
	{
		if (!isPreview)
			return (await issuance.GetCurrentAsync(salarySlip.Id)).Pdf;
		var employee = await employeeRepository.GetAsync(salarySlip.EmployeeId, includeDetails: false);
		var dto = await salarySlipDtoMapper.ToDtoAsync(salarySlip, employee);
		var school = await issuance.EnsureSchoolConfiguredAsync();
		var pdfModel = await issuance.PrepareModelAsync(dto, school);
		pdfModel = pdfModel with { IsPreview = true, DocumentLabel = "Preview - " + dto.Status };

		return payslipPdfService.Generate(pdfModel);
	}
}