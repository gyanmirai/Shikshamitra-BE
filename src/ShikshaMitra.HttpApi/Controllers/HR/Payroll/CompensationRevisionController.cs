using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/employees/{employeeId:guid}/compensation-revisions")]
[Authorize]
public class CompensationRevisionController(
	ICompensationRevisionAppService appService,
	IEmployeeAuthorizationService employeeAuthorizationService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View)]
	public async Task<CompensationRevisionListDto> GetListAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);
		return await appService.GetListAsync(employeeId);
	}

	[HttpGet("effective")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View)]
	public async Task<CompensationRevisionDto?> GetEffectiveAsync(Guid employeeId, [FromQuery] DateTime month)
	{
		await employeeAuthorizationService.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);
		return await appService.GetEffectiveAsync(employeeId, month);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Create)]
	public async Task<ActionResult<CompensationRevisionDto>> CreateAsync(
		Guid employeeId,
		[FromBody] CreateCompensationRevisionDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		return StatusCode(201, await appService.CreateAsync(employeeId, input));
	}

	[HttpPut("{revisionId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Edit)]
	public async Task<CompensationRevisionDto> UpdateAsync(
		Guid employeeId,
		Guid revisionId,
		[FromBody] UpdateCompensationRevisionDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		return await appService.UpdateAsync(employeeId, revisionId, input);
	}

	[HttpPost("{revisionId:guid}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Submit)]
	public async Task<CompensationRevisionDto> SubmitAsync(Guid employeeId, Guid revisionId)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		return await appService.SubmitAsync(employeeId, revisionId);
	}

	[HttpPost("{revisionId:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Approve)]
	public async Task<CompensationRevisionDto> ApproveAsync(Guid employeeId, Guid revisionId)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		return await appService.ApproveAsync(employeeId, revisionId);
	}

	[HttpPost("{revisionId:guid}/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Approve)]
	public async Task<CompensationRevisionDto> RejectAsync(
		Guid employeeId,
		Guid revisionId,
		[FromBody] RejectCompensationRevisionDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		return await appService.RejectAsync(employeeId, revisionId, input);
	}
}
