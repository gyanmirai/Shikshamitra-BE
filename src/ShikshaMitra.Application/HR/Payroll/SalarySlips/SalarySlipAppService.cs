using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class SalarySlipAppService(SalarySlipWorkflow workflow, SalarySlipPdfWorkflow pdfWorkflow,
	SalarySlipAvailabilityReader availability)
	: ApplicationService, ISalarySlipAppService
{
	public async Task<SalarySlipAvailabilityDto> GetAvailabilityAsync(Guid employeeId, string monthYear, Guid? academicYearId = null)
	{
		await AuthorizationService.CheckAsync(Permissions.View);
		return await availability.GetAsync(employeeId, monthYear, academicYearId);
	}
	public async Task<SalarySlipDto> GetSalarySlipAsync(Guid employeeId, string monthYear)
	{
		await AuthorizationService.CheckAsync(Permissions.View);
		return await workflow.GetSalarySlipAsync(employeeId, monthYear);
	}
	public async Task<byte[]> GenerateSalarySlipPdfAsync(Guid employeeId, string monthYear)
	{
		await AuthorizationService.CheckAsync(Permissions.View);
		return await pdfWorkflow.GenerateSalarySlipPdfAsync(employeeId, monthYear);
	}
	public async Task<byte[]> GenerateSalarySlipPreviewPdfAsync(Guid employeeId, string monthYear)
	{
		await AuthorizationService.CheckAsync(Permissions.View);
		return await pdfWorkflow.GenerateSalarySlipPreviewPdfAsync(employeeId, monthYear);
	}
	public async Task<SalarySlipDto> GenerateSalarySlipAsync(GenerateSalarySlipInput input)
	{
		await AuthorizationService.CheckAsync(Permissions.Add);
		return await workflow.GenerateSalarySlipAsync(input);
	}
	public async Task<SalarySlipDto> GetAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(Permissions.View);
		return await workflow.GetAsync(id);
	}
	public async Task<SalarySlipDto> UpdateAsync(Guid id, UpdateSalarySlipInput input)
	{
		await AuthorizationService.CheckAsync(Permissions.Add);
		return await workflow.UpdateAsync(id, input);
	}
	public async Task<SalarySlipDto> FinalizeAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(Permissions.Add);
		return await workflow.FinalizeAsync(id);
	}
	public async Task DeleteAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(Permissions.Delete);
		await workflow.DeleteAsync(id);
	}
}
