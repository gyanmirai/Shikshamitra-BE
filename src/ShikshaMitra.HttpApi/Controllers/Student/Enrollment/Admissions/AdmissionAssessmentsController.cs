using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/assessments")]
public class AdmissionAssessmentsController(IAdmissionAssessmentAppService service) : AbpController
{
	[HttpGet("scopes"), Authorize(Permissions.View)]
	public async Task<Result<List<AdmissionAssessmentScopeDto>>> GetScopesAsync() => Result.Success(await service.GetScopesAsync());
	[HttpGet("sessions"), Authorize(Permissions.View)]
	public async Task<Result<List<AdmissionAssessmentSessionDto>>> GetSessionsAsync([FromQuery] Guid admissionOfferingId, [FromQuery] Guid workflowStageDefinitionId) =>
		Result.Success(await service.GetSessionsAsync(admissionOfferingId, workflowStageDefinitionId));
	[HttpPost("sessions"), Authorize(Permissions.ManageSessions)]
	public async Task<Result<AdmissionAssessmentSessionDto>> CreateSessionAsync(AdmissionAssessmentSessionInput input) => Result.Success(await service.CreateSessionAsync(input));
	[HttpPut("sessions/{id:guid}"), Authorize(Permissions.ManageSessions)]
	public async Task<Result<AdmissionAssessmentSessionDto>> UpdateSessionAsync(Guid id, AdmissionAssessmentSessionInput input) => Result.Success(await service.UpdateSessionAsync(id, input));
	[HttpPost("sessions/{id:guid}/cancel"), Authorize(Permissions.ManageSessions)]
	public async Task<Result<AdmissionAssessmentSessionDto>> CancelSessionAsync(Guid id, AdmissionAssessmentActionInput input) => Result.Success(await service.CancelSessionAsync(id, input));
	[HttpGet("applications/{applicationId:guid}/stages/{stageInstanceId:guid}"), Authorize(Permissions.View)]
	public async Task<Result<AdmissionAssessmentDto>> GetAsync(Guid applicationId, Guid stageInstanceId) => Result.Success(await service.GetAsync(applicationId, stageInstanceId));
	[HttpPost("applications/{applicationId:guid}/stages/{stageInstanceId:guid}/attempts"), Authorize(Permissions.Book)]
	public async Task<Result<AdmissionAssessmentDto>> BookAsync(Guid applicationId, Guid stageInstanceId, AdmissionAssessmentBookingInput input) => Result.Success(await service.BookAsync(applicationId, stageInstanceId, input));
	[HttpPut("attempts/{id:guid}/attendance"), Authorize(Permissions.RecordAttendance)]
	public async Task<Result<AdmissionAssessmentDto>> RecordAttendanceAsync(Guid id, AdmissionAssessmentAttendanceInput input) => Result.Success(await service.RecordAttendanceAsync(id, input));
	[HttpPut("attempts/{id:guid}/result"), Authorize(Permissions.RecordResults)]
	public async Task<Result<AdmissionAssessmentDto>> RecordResultAsync(Guid id, AdmissionAssessmentResultInput input) => Result.Success(await service.RecordResultAsync(id, input));
	[HttpPost("attempts/{id:guid}/review"), Authorize(Permissions.Review)]
	public async Task<Result<AdmissionAssessmentDto>> ReviewAsync(Guid id, AdmissionAssessmentReviewInput input) => Result.Success(await service.ReviewAsync(id, input));
	[HttpPost("attempts/{id:guid}/retake"), Authorize(Permissions.Book)]
	public async Task<Result<AdmissionAssessmentDto>> RetakeAsync(Guid id, AdmissionAssessmentActionInput input) => Result.Success(await service.RetakeAsync(id, input));
	[HttpPost("attempts/{id:guid}/cancel"), Authorize(Permissions.Book)]
	public async Task<Result<AdmissionAssessmentDto>> CancelAttemptAsync(Guid id, AdmissionAssessmentActionInput input) => Result.Success(await service.CancelAttemptAsync(id, input));
	[HttpPost("applications/{applicationId:guid}/stages/{stageInstanceId:guid}/waive"), Authorize(Permissions.Waive)]
	public async Task<Result<AdmissionAssessmentDto>> WaiveAsync(Guid applicationId, Guid stageInstanceId, AdmissionAssessmentWaiverInput input) => Result.Success(await service.WaiveAsync(applicationId, stageInstanceId, input));
}
