using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipPdfWorkflow(SalarySlipIssuedReader issuedReader, PayslipIssuanceService issuance)
	: ApplicationService
{
	public async Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear)
	{
		var salarySlip = await issuedReader.GetAsync(employeeId, monthYear);
		return (await issuance.GetCurrentAsync(salarySlip.Id)).Pdf;
	}

	// Retain existing URLs while enforcing the same issued-only boundary as official downloads.
	public Task<byte[]> GenerateSalarySlipPreviewPdfAsync(Guid employeeId, string monthYear) =>
		GenerateSalarySlipPdfAsync(employeeId, monthYear);
}
