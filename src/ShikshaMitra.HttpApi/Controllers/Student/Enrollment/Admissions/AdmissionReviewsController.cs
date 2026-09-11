using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionReviews;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;

[ApiController, Authorize, Route("api/student/enrollment/admissions/reviews")]
public class AdmissionReviewsController(IAdmissionReviewAppService service) : AbpController
{
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionReviewDto>> GetAsync(Guid applicationId, Guid stageId) => Result.Success(await service.GetAsync(applicationId, stageId));
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}/history"), Authorize(Permissions.View)]
	public async Task<Result<List<AdmissionReviewRecordDto>>> GetHistoryAsync(Guid applicationId, Guid stageId) => Result.Success(await service.GetHistoryAsync(applicationId, stageId));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/draft"), Authorize(Permissions.View), Authorize(Permissions.Edit)]
	public async Task<Result<AdmissionReviewDto>> SaveDraftAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input) => Result.Success(await service.SaveDraftAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/submit"), Authorize(Permissions.View), Authorize(Permissions.Submit)]
	public async Task<Result<AdmissionReviewDto>> SubmitAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input) => Result.Success(await service.SubmitAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/assignment"), Authorize(Permissions.View), Authorize(Permissions.Assign)]
	public async Task<Result<AdmissionReviewDto>> AssignAsync(Guid applicationId, Guid stageId, AdmissionReviewAssignmentDto input) => Result.Success(await service.AssignAsync(applicationId, stageId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/request-correction"), Authorize(Permissions.View), Authorize(Permissions.Decide), Authorize(ApplicationPermissions.Reopen), Authorize(ApplicationPermissions.Decide)]
	public async Task<Result<AdmissionReviewDto>> RequestCorrectionAsync(Guid applicationId, Guid stageId, AdmissionReviewCorrectionDto input) => Result.Success(await service.RequestCorrectionAsync(applicationId, stageId, input));
	[HttpGet("queue"), Authorize(Permissions.View)]
	public async Task<Result<PagedResultDto<AdmissionReviewQueueItemDto>>> GetQueueAsync([FromQuery] AdmissionReviewQueueInput input) => Result.Success(await service.GetQueueAsync(input));
	[HttpGet("reviewer-roles"), Authorize(WorkflowPermissions.View)]
	public async Task<Result<List<AdmissionEligibilityRoleDto>>> GetReviewerRolesAsync() => Result.Success(await service.GetReviewerRolesAsync());
	[HttpGet("reviewers")]
	public async Task<Result<List<AdmissionReviewUserDto>>> GetReviewersAsync(string? filter = null, string? role = null) => Result.Success(await service.GetReviewersAsync(filter, role));
}
