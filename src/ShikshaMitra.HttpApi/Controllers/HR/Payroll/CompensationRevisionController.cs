using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/employees/{employeeId:guid}/compensation")]
[Authorize]
public class CompensationRevisionController(
	ICompensationRevisionAppService appService,
	IEmployeeAuthorizationService employeeAuthorizationService) : AbpControllerBase
{
	[HttpGet]
	public async Task<CompensationRevisionListDto> GetListAsync(Guid employeeId)
	{
		await CheckReadPermissionAsync();
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		return await appService.GetListAsync(employeeId);
	}

	[HttpGet("effective")]
	public async Task<CompensationRevisionDto?> GetEffectiveAsync(Guid employeeId, [FromQuery] DateTime month)
	{
		await CheckReadPermissionAsync();
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		return await appService.GetEffectiveAsync(employeeId, month);
	}

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage)]
	public async Task<CompensationRevisionDto> SaveAsync(
		Guid employeeId,
		[FromBody] SaveEmployeeCompensationDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		return await appService.SaveAsync(employeeId, input);
	}

	[HttpPost("protection-migration/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage)]
	public async Task<ProtectionMigrationPreviewDto> PreviewProtectionMigrationAsync(Guid employeeId, [FromBody] PreviewProtectionMigrationDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		return await appService.PreviewProtectionMigrationAsync(employeeId, input);
	}

	[HttpPost("protection-migration/apply")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage)]
	public async Task<CompensationRevisionDto> ApplyProtectionMigrationAsync(Guid employeeId, [FromBody] ApplyProtectionMigrationDto input)
	{
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		return await appService.ApplyProtectionMigrationAsync(employeeId, input);
	}

	private async Task CheckReadPermissionAsync()
	{
		if (await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View) ||
			await AuthorizationService.IsGrantedAsync(
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage))
		{
			return;
		}

		throw new AbpAuthorizationException("Salary history requires compensation View or Manage permission.");
	}
}
