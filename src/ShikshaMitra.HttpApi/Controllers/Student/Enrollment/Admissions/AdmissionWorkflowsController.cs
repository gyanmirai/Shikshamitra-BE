using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/workflows")]
public class AdmissionWorkflowsController(IAdmissionWorkflowAppService appService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<PagedResultDto<AdmissionWorkflowDto>>> GetList([FromQuery] AdmissionWorkflowListInput input) => Result.Success(await appService.GetListAsync(input));
	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<AdmissionWorkflowDto>> Get(Guid id) => Result.Success(await appService.GetAsync(id));
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Create)]
	public async Task<Result<AdmissionWorkflowDto>> Create(CreateUpdateAdmissionWorkflowDto input) => Result.Success(await appService.CreateAsync(input));
	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Edit)]
	public async Task<Result<AdmissionWorkflowDto>> Update(Guid id, CreateUpdateAdmissionWorkflowDto input) => Result.Success(await appService.UpdateAsync(id, input));
	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Delete)]
	public async Task<Result> Delete(Guid id, [FromQuery] string? concurrencyStamp = null)
	{
		await appService.DeleteAsync(id, concurrencyStamp);
		return Result.Success();
	}
	[HttpPost("{id:guid}/clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Clone)]
	public async Task<Result<AdmissionWorkflowDto>> Clone(Guid id, CloneAdmissionWorkflowDto input) => Result.Success(await appService.CloneAsync(id, input));
	[HttpPost("{id:guid}/new-version")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.CreateVersion)]
	public async Task<Result<AdmissionWorkflowDto>> CreateNewVersion(Guid id) => Result.Success(await appService.CreateNewVersionAsync(id));
	[HttpPost("{id:guid}/validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Validate)]
	public async Task<Result<AdmissionWorkflowValidationResultDto>> Validate(Guid id) => Result.Success(await appService.ValidateAsync(id));
	[HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public async Task<Result<AdmissionWorkflowDto>> Publish(Guid id, [FromQuery] string? concurrencyStamp = null) => Result.Success(await appService.PublishAsync(id, concurrencyStamp));
	[HttpPost("{id:guid}/governance/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public async Task<Result<AdmissionWorkflowDto>> PublishWithGovernance(Guid id, PublishAdmissionWorkflowDto input) => Result.Success(await appService.PublishWithGovernanceAsync(id, input));
	[HttpPost("{id:guid}/submit-for-approval")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.SubmitForApproval)]
	public async Task<Result<AdmissionWorkflowDto>> SubmitForApproval(Guid id, SubmitAdmissionWorkflowDto input) => Result.Success(await appService.SubmitForApprovalAsync(id, input));
	[HttpPost("{id:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve)]
	public async Task<Result<AdmissionWorkflowDto>> Approve(Guid id, DecideAdmissionWorkflowDto input) => Result.Success(await appService.ApproveAsync(id, input));
	[HttpPost("{id:guid}/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve)]
	public async Task<Result<AdmissionWorkflowDto>> Reject(Guid id, DecideAdmissionWorkflowDto input) => Result.Success(await appService.RejectAsync(id, input));
	[HttpPost("{id:guid}/rollback-draft")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Rollback)]
	public async Task<Result<AdmissionWorkflowDto>> CreateRollbackDraft(Guid id, RollbackAdmissionWorkflowDto input) => Result.Success(await appService.CreateRollbackDraftAsync(id, input));
	[HttpPost("{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Archive)]
	public async Task<Result<AdmissionWorkflowDto>> Archive(Guid id, [FromQuery] string? concurrencyStamp = null) => Result.Success(await appService.ArchiveAsync(id, concurrencyStamp));
	[HttpPost("{id:guid}/reopen")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public async Task<Result<AdmissionWorkflowDto>> Reopen(Guid id, [FromQuery] string? concurrencyStamp = null) => Result.Success(await appService.ReopenAsync(id, concurrencyStamp));
	[HttpGet("{id:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<System.Collections.Generic.List<AdmissionWorkflowDto>>> GetVersionHistory(Guid id) => Result.Success(await appService.GetVersionHistoryAsync(id));
	[HttpGet("{id:guid}/usage")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ViewUsage)]
	public async Task<Result<AdmissionWorkflowUsageDto>> GetUsage(Guid id) => Result.Success(await appService.GetUsageAsync(id));
	[HttpGet("stage-types")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<System.Collections.Generic.IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto>>> GetStageTypes() => Result.Success(await appService.GetStageTypesAsync());
	[HttpGet("governance/settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<AdmissionWorkflowGovernanceSettingDto>> GetGovernanceSettings() => Result.Success(await appService.GetGovernanceSettingsAsync());
	[HttpPut("governance/settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ManageGovernance)]
	public async Task<Result<AdmissionWorkflowGovernanceSettingDto>> UpdateGovernanceSettings(UpdateAdmissionWorkflowGovernanceSettingDto input) => Result.Success(await appService.UpdateGovernanceSettingsAsync(input));
	[HttpGet("governance/approvers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<List<AdmissionWorkflowApproverDto>>> GetApprovers([FromQuery] string? filter = null) => Result.Success(await appService.GetApproversAsync(filter));
	[HttpGet("{id:guid}/governance/history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<List<AdmissionWorkflowGovernanceHistoryDto>>> GetGovernanceHistory(Guid id) => Result.Success(await appService.GetGovernanceHistoryAsync(id));
	[HttpGet("governance/notifications")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result<List<AdmissionWorkflowNotificationDto>>> GetNotifications([FromQuery] bool unreadOnly = false) => Result.Success(await appService.GetNotificationsAsync(unreadOnly));
	[HttpPost("governance/notifications/{notificationId:guid}/read")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public async Task<Result> MarkNotificationRead(Guid notificationId)
	{
		await appService.MarkNotificationReadAsync(notificationId);
		return Result.Success();
	}
}
