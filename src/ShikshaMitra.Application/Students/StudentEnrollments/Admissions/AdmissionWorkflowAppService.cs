using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionWorkflowAppService(
	AdmissionWorkflowReadService readService,
	AdmissionWorkflowCommandService commandService,
	AdmissionWorkflowGovernanceService governanceService)
	: ApplicationService, IAdmissionWorkflowAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<PagedResultDto<AdmissionWorkflowDto>> GetListAsync(AdmissionWorkflowListInput input) =>
		readService.GetListAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<AdmissionWorkflowDto> GetAsync(Guid id) => readService.GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Create)]
	public Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input) =>
		commandService.CreateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Edit)]
	public Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input) =>
		commandService.UpdateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Delete)]
	public Task DeleteAsync(Guid id, string? concurrencyStamp = null) => commandService.DeleteAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Clone)]
	public Task<AdmissionWorkflowDto> CloneAsync(Guid id, CloneAdmissionWorkflowDto input) =>
		commandService.CloneAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.CreateVersion)]
	public Task<AdmissionWorkflowDto> CreateNewVersionAsync(Guid id) =>
		commandService.CreateNewVersionAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Validate)]
	public Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id) =>
		commandService.ValidateAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public Task<AdmissionWorkflowDto> PublishAsync(Guid id, string? concurrencyStamp = null) =>
		commandService.PublishAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public Task<AdmissionWorkflowDto> PublishWithGovernanceAsync(Guid id, PublishAdmissionWorkflowDto input) =>
		commandService.PublishWithGovernanceAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.SubmitForApproval)]
	public Task<AdmissionWorkflowDto> SubmitForApprovalAsync(Guid id, SubmitAdmissionWorkflowDto input) =>
		commandService.SubmitForApprovalAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve)]
	public Task<AdmissionWorkflowDto> ApproveAsync(Guid id, DecideAdmissionWorkflowDto input) =>
		commandService.ApproveAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve)]
	public Task<AdmissionWorkflowDto> RejectAsync(Guid id, DecideAdmissionWorkflowDto input) =>
		commandService.RejectAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Rollback)]
	public Task<AdmissionWorkflowDto> CreateRollbackDraftAsync(Guid id, RollbackAdmissionWorkflowDto input) =>
		commandService.CreateRollbackDraftAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Archive)]
	public Task<AdmissionWorkflowDto> ArchiveAsync(Guid id, string? concurrencyStamp = null) => commandService.ArchiveAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish)]
	public Task<AdmissionWorkflowDto> ReopenAsync(Guid id, string? concurrencyStamp = null) => commandService.ReopenAsync(id, concurrencyStamp);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<List<AdmissionWorkflowDto>> GetVersionHistoryAsync(Guid id) =>
		readService.GetVersionHistoryAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ViewUsage)]
	public Task<AdmissionWorkflowUsageDto> GetUsageAsync(Guid id) => readService.GetUsageAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto>> GetStageTypesAsync() =>
		Task.FromResult(readService.GetStageTypes());

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<AdmissionWorkflowGovernanceSettingDto> GetGovernanceSettingsAsync() =>
		governanceService.GetSettingsAsync();

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ManageGovernance)]
	public Task<AdmissionWorkflowGovernanceSettingDto> UpdateGovernanceSettingsAsync(UpdateAdmissionWorkflowGovernanceSettingDto input) =>
		governanceService.UpdateSettingsAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<List<AdmissionWorkflowApproverDto>> GetApproversAsync(string? filter = null) =>
		governanceService.GetApproversAsync(filter);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<List<AdmissionWorkflowGovernanceHistoryDto>> GetGovernanceHistoryAsync(Guid id) =>
		governanceService.GetHistoryAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task<List<AdmissionWorkflowNotificationDto>> GetNotificationsAsync(bool unreadOnly = false) =>
		governanceService.GetNotificationsAsync(unreadOnly);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View)]
	public Task MarkNotificationReadAsync(Guid notificationId) =>
		governanceService.MarkNotificationReadAsync(notificationId);
}
