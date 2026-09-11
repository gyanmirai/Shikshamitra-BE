using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionReviews;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionReviewAppService(AdmissionReviewReadService reader, AdmissionReviewCommandService commands,
	AdmissionReviewPeopleService people, AdmissionReviewQueueService queue, IPermissionChecker permissions) : ApplicationService, IAdmissionReviewAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionReviewDto> GetAsync(Guid applicationId, Guid stageId) => reader.GetAsync(applicationId, stageId);
	[Authorize(Permissions.View)]
	public Task<List<AdmissionReviewRecordDto>> GetHistoryAsync(Guid applicationId, Guid stageId) => reader.GetHistoryAsync(applicationId, stageId);
	[Authorize(Permissions.View), Authorize(Permissions.Edit)]
	public async Task<AdmissionReviewDto> SaveDraftAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input)
	{ await commands.SaveAsync(applicationId, stageId, input, false); return await reader.GetAsync(applicationId, stageId); }
	[Authorize(Permissions.View), Authorize(Permissions.Submit)]
	public async Task<AdmissionReviewDto> SubmitAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input)
	{ await commands.SaveAsync(applicationId, stageId, input, true); return await reader.GetAsync(applicationId, stageId); }
	[Authorize(Permissions.View), Authorize(Permissions.Assign)]
	public async Task<AdmissionReviewDto> AssignAsync(Guid applicationId, Guid stageId, AdmissionReviewAssignmentDto input)
	{ await commands.AssignAsync(applicationId, stageId, input); return await reader.GetAsync(applicationId, stageId); }
	[Authorize(Permissions.View), Authorize(Permissions.Decide), Authorize(ApplicationPermissions.Reopen), Authorize(ApplicationPermissions.Decide)]
	public async Task<AdmissionReviewDto> RequestCorrectionAsync(Guid applicationId, Guid stageId, AdmissionReviewCorrectionDto input)
	{ await commands.RequestCorrectionAsync(applicationId, stageId, input); return await reader.GetAsync(applicationId, stageId); }
	[Authorize(Permissions.View)]
	public Task<PagedResultDto<AdmissionReviewQueueItemDto>> GetQueueAsync(AdmissionReviewQueueInput input) => queue.GetAsync(input);
	[Authorize(WorkflowPermissions.View)]
	public Task<List<AdmissionEligibilityRoleDto>> GetReviewerRolesAsync() => people.GetRolesAsync();
	public async Task<List<AdmissionReviewUserDto>> GetReviewersAsync(string? filter = null, string? role = null)
	{
		if (!await permissions.IsGrantedAsync(WorkflowPermissions.View) && !await permissions.IsGrantedAsync(Permissions.Assign)) throw new AbpAuthorizationException();
		return await people.GetUsersAsync(filter, role);
	}
}
