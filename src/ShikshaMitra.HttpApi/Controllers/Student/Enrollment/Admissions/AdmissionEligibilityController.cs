using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionEligibility;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Authorize]
[Route("api/student/enrollment/admissions/eligibility")]
public class AdmissionEligibilityController(IAdmissionEligibilityAppService service) : AbpController
{
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionEligibilityDto>> GetAsync(Guid applicationId, Guid stageId) => Result.Success(await service.GetAsync(applicationId, stageId));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/evaluate"), Authorize(Permissions.View), Authorize(Permissions.Evaluate)]
	public async Task<Result<AdmissionEligibilityDto>> EvaluateAsync(Guid applicationId, Guid stageId, AdmissionEligibilityEvaluateDto input) => Result.Success(await service.EvaluateAsync(applicationId, stageId, input));
	[HttpGet("applications/{applicationId:guid}/stages/{stageId:guid}/history"), Authorize(Permissions.View)]
	public async Task<Result<List<AdmissionEligibilityEvaluationDto>>> GetHistoryAsync(Guid applicationId, Guid stageId) => Result.Success(await service.GetHistoryAsync(applicationId, stageId));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/evaluations/{evaluationId:guid}/exceptions"), Authorize(Permissions.View), Authorize(Permissions.RequestException)]
	public async Task<Result<AdmissionEligibilityDto>> RequestExceptionAsync(Guid applicationId, Guid stageId, Guid evaluationId, AdmissionEligibilityExceptionRequestDto input) => Result.Success(await service.RequestExceptionAsync(applicationId, stageId, evaluationId, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageId:guid}/exceptions/{exceptionId:guid}/review"), Authorize(Permissions.View), Authorize(Permissions.ReviewException)]
	public async Task<Result<AdmissionEligibilityDto>> ReviewExceptionAsync(Guid applicationId, Guid stageId, Guid exceptionId, AdmissionEligibilityExceptionReviewDto input) => Result.Success(await service.ReviewExceptionAsync(applicationId, stageId, exceptionId, input));
	[HttpGet("reviewer-roles"), Authorize(WorkflowPermissions.View)]
	public async Task<Result<List<AdmissionEligibilityRoleDto>>> GetReviewerRolesAsync() => Result.Success(await service.GetReviewerRolesAsync());
}
