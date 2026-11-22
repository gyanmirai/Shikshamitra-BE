using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Permission = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll;

namespace ShikshaMitra.Controllers.HR.Payroll.RunPayroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/run")]
[Authorize(Permission.View)]
public class PayrollApprovalController(IPayrollApprovalAppService service) : AbpControllerBase
{
	[HttpGet("approval-settings")]
	public Task<PayrollApprovalSettingsDto> GetSettingsAsync() => service.GetSettingsAsync();

	[HttpGet("approval-settings/approvers")]
	[Authorize(Permission.ManageWorkflow)]
	public Task<List<PayrollApproverDto>> GetApproversAsync([FromQuery, MaxLength(100)] string? search) => service.GetApproversAsync(search);

	[HttpPut("approval-settings")]
	[Authorize(Permission.ManageWorkflow)]
	public Task<PayrollApprovalSettingsDto> ConfigureAsync([FromBody] ConfigurePayrollApprovalCommand command) =>
		service.ConfigureAsync(command);

	[HttpGet("approval-settings/section-options")]
	[Authorize(Permission.ManageWorkflow)]
	public Task<PayrollSectionOptionsDto> GetSectionOptionsAsync() => service.GetSectionOptionsAsync();

	[HttpGet("approval-settings/section-reviewers")]
	[Authorize(Permission.ManageWorkflow)]
	public Task<List<PayrollApproverDto>> GetSectionReviewersAsync([FromQuery] Guid stageId, [FromQuery] Guid roleId) =>
		service.GetSectionReviewersAsync(stageId, roleId);

	[HttpPut("approval-settings/sections")]
	[Authorize(Permission.ManageWorkflow)]
	public Task<PayrollApprovalSettingsDto> ConfigureSectionsAsync([FromBody] ConfigurePayrollSectionsCommand command) =>
		service.ConfigureSectionsAsync(command);

	[HttpPost("approval-settings/{actionName:regex(^(activate|pause|resume)$)}")]
	[Authorize(Permission.ManageWorkflow)]
	public Task<PayrollApprovalSettingsDto> ChangeModeAsync(string actionName, [FromBody] PayrollApprovalSettingsCommand command) =>
		service.ChangeModeAsync(actionName, command);

	[HttpGet("submissions")]
	public Task<List<PayrollSubmissionDto>> GetListAsync([FromQuery] PayrollPreviewMonthInput input) => service.GetListAsync(input);

	[HttpGet("submissions/{id:guid}")]
	public Task<PayrollSubmissionDetailDto> GetAsync(Guid id) => service.GetAsync(id);

	[HttpPost("submissions")]
	[Authorize(Permission.Submit)]
	public Task<PayrollSubmissionDto> SubmitAsync([FromBody] SubmitPayrollCommand command) => service.SubmitAsync(command);

	[HttpPost("section-reviews")]
	[Authorize(Permission.Submit)]
	public Task<List<PayrollSubmissionDto>> SubmitSectionsAsync([FromBody] SubmitPayrollCommand command) =>
		service.SubmitSectionsAsync(command);

	[HttpPost("submissions/{id:guid}/{actionName:regex(^(finalize|withdraw|return|return-stale|review)$)}")]
	public Task<PayrollSubmissionDto> ResolveAsync(Guid id, string actionName, [FromBody] ResolvePayrollSubmissionCommand command) =>
		service.ResolveAsync(id, actionName, command);
}
