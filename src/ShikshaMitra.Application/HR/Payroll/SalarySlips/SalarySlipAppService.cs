using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipAppService(
	SalarySlipWorkflow workflow,
	SalarySlipPdfWorkflow pdfWorkflow)
	: ApplicationService, ISalarySlipAppService
{
	public Task<SalarySlipDto> GetSalarySlipAsync(Guid employeeId, string monthYear) =>
		workflow.GetSalarySlipAsync(employeeId, monthYear);

	public Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear) =>
		pdfWorkflow.GenerateSalarySlipPdfAsync(employeeId, monthYear);

	public Task<SalarySlipDto> GenerateSalarySlipAsync(GenerateSalarySlipInput input) =>
		workflow.GenerateSalarySlipAsync(input);

	public Task<SalarySlipDto> GetAsync(Guid id) =>
		workflow.GetAsync(id);

	public Task<SalarySlipDto> UpdateAsync(Guid id, UpdateSalarySlipInput input) =>
		workflow.UpdateAsync(id, input);

	public Task<SalarySlipDto> FinalizeAsync(Guid id) =>
		workflow.FinalizeAsync(id);

	public Task DeleteAsync(Guid id) =>
		workflow.DeleteAsync(id);
}
