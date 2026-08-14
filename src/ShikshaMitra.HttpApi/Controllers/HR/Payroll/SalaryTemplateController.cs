using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/salary-templates")]
[Authorize]
public class SalaryTemplateController(ISalaryTemplateAppService appService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public Task<SalaryTemplateListDto> GetListAsync() => appService.GetListAsync();

	[HttpGet("published")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add)]
	public Task<SalaryTemplateListDto> GetPublishedListAsync() => appService.GetPublishedListAsync();

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View)]
	public Task<SalaryTemplateDto> GetAsync(Guid id) => appService.GetAsync(id);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add)]
	public async Task<ActionResult<SalaryTemplateDto>> CreateAsync(CreateSalaryTemplateDto input) =>
		StatusCode(201, await appService.CreateAsync(input));

	[HttpPost("{id:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit)]
	public Task<SalaryTemplateDto> CreateNextVersionAsync(Guid id) => appService.CreateNextVersionAsync(id);

	[HttpPut("{id:guid}/versions/{versionId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit)]
	public Task<SalaryTemplateDto> UpdateDraftAsync(
		Guid id,
		Guid versionId,
		UpdateSalaryTemplateDraftDto input) => appService.UpdateDraftAsync(id, versionId, input);

	[HttpPost("{id:guid}/versions/{versionId:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Publish)]
	public Task<SalaryTemplateDto> PublishAsync(Guid id, Guid versionId) => appService.PublishAsync(id, versionId);

	[HttpPost("{id:guid}/clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add)]
	public Task<SalaryTemplateDto> CloneAsync(Guid id, [FromQuery] string name) => appService.CloneAsync(id, name);

	[HttpPost("{id:guid}/set-default")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.SetDefault)]
	public Task<SalaryTemplateDto> SetDefaultAsync(Guid id) => appService.SetDefaultAsync(id);

	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Archive)]
	public async Task<IActionResult> ArchiveAsync(Guid id)
	{
		await appService.ArchiveAsync(id);
		return NoContent();
	}

	[HttpPost("versions/{templateVersionId:guid}/calculate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add)]
	public Task<SalaryTemplateCalculationDto> CalculateAsync(
		Guid templateVersionId,
		CalculateSalaryTemplateRequestDto input) => appService.CalculateAsync(templateVersionId, input);
}
