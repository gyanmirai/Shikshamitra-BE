using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/statutory-schemes/esi")]
[Authorize]
public class EsiStatutoryController(
	IEsiStatutoryAppService appService,
	IEmployeeAuthorizationService employeeAuthorizationService) : AbpControllerBase
{
	[HttpGet("rule")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View)]
	public Task<StatutoryRuleVersionDto> GetRuleAsync([FromQuery] DateTime? effectiveOn = null) =>
		appService.GetCurrentRuleAsync(effectiveOn);

	[HttpGet("establishment")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View)]
	public Task<StatutoryEstablishmentDto> GetEstablishmentAsync() => appService.GetEstablishmentAsync();

	[HttpPut("establishment")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Edit)]
	public Task<StatutoryEstablishmentDto> ConfigureEstablishmentAsync(
		[FromBody] ConfigureStatutoryEstablishmentDto input) => appService.ConfigureEstablishmentAsync(input);

	[HttpGet("component-treatments")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View)]
	public Task<List<PayrollComponentStatutoryTreatmentDto>> GetComponentTreatmentsAsync(
		[FromQuery] DateTime? effectiveOn = null) => appService.GetComponentTreatmentsAsync(effectiveOn);

	[HttpPut("component-treatments")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Edit)]
	public Task<List<PayrollComponentStatutoryTreatmentDto>> UpdateComponentTreatmentsAsync(
		[FromBody] UpdatePayrollComponentStatutoryTreatmentsDto input) => appService.UpdateComponentTreatmentsAsync(input);

	[HttpGet("employees/{employeeId:guid}/enrollment")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View)]
	public async Task<EmployeeStatutoryEnrollmentDto> GetEnrollmentAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View);
		return await appService.GetEnrollmentAsync(employeeId);
	}

	[HttpPut("employees/{employeeId:guid}/enrollment")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ManageEnrollment)]
	public async Task<EmployeeStatutoryEnrollmentDto> UpdateEnrollmentAsync(
		Guid employeeId,
		[FromBody] UpdateEmployeeStatutoryEnrollmentDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ManageEnrollment);
		return await appService.UpdateEnrollmentAsync(employeeId, input);
	}

	[HttpPost("employees/{employeeId:guid}/overrides")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ApproveOverride)]
	public async Task<IActionResult> CreateOverrideAsync(Guid employeeId, [FromBody] CreateStatutoryOverrideDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ApproveOverride);
		await appService.CreateOverrideAsync(employeeId, input);
		return NoContent();
	}

	[HttpGet("employees/{employeeId:guid}/overrides")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View)]
	public async Task<List<StatutoryOverrideDto>> GetOverridesAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeScopedPermissionAsync(employeeId,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View);
		return await appService.GetOverridesAsync(employeeId);
	}

	[HttpPost("impact-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View)]
	public Task<StatutoryImpactPreviewDto> PreviewAsync([FromBody] StatutoryImpactPreviewRequestDto input) =>
		appService.PreviewAsync(input);

	[HttpPost("activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Activate)]
	[ProducesResponseType(typeof(StatutoryEstablishmentDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public Task<StatutoryEstablishmentDto> ActivateAsync([FromBody] ActivateStatutorySchemeDto input) =>
		appService.ActivateAsync(input);
}
