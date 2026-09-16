using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/salary-templates")]
[Authorize]
public class SalaryTemplateController(
	ISalaryTemplateAppService appService,
	IEmployeeAuthorizationService employeeAuthorizationService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public Task<SalaryTemplateListDto> GetListAsync() => appService.GetListAsync();

	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage)]
	public Task<SalaryTemplateListDto> GetActiveListAsync() => appService.GetActiveListAsync();

	[HttpGet("component-catalog")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public Task<SalaryTemplateComponentCatalogDto> GetComponentCatalogAsync() =>
		appService.GetComponentCatalogAsync();

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public Task<SalaryTemplateDto> GetAsync(Guid id) => appService.GetAsync(id);

	[HttpGet("{id:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public Task<SalaryTemplateVersionListDto> GetVersionsAsync(Guid id) => appService.GetVersionsAsync(id);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add)]
	public async Task<ActionResult<SalaryTemplateDto>> CreateAsync(SaveSalaryTemplateDto input) =>
		StatusCode(201, await appService.CreateAsync(input));

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit)]
	public Task<SalaryTemplateDto> UpdateAsync(Guid id, SaveSalaryTemplateDto input) => appService.UpdateAsync(id, input);

	[HttpPost("{id:guid}/set-default")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.SetDefault)]
	public Task<SalaryTemplateDto> SetDefaultAsync(Guid id) => appService.SetDefaultAsync(id);

	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Archive)]
	public async Task<IActionResult> ArchiveAsync(Guid id, [FromQuery] Guid? replacementDefaultTemplateId)
	{
		await appService.ArchiveAsync(id, new ArchiveSalaryTemplateDto
		{
			ReplacementDefaultTemplateId = replacementDefaultTemplateId
		});
		return NoContent();
	}

	[HttpPost("versions/{templateVersionId:guid}/calculate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage)]
	public async Task<SalaryTemplateCalculationDto> CalculateAsync(
		Guid templateVersionId,
		CalculateSalaryTemplateRequestDto input)
	{
		if (input.EmployeeId.HasValue)
		{
			await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(input.EmployeeId.Value);
		}

		return await appService.CalculateAsync(templateVersionId, input);
	}
}
