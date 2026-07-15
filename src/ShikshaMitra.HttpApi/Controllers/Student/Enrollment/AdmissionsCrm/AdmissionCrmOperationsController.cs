using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmOperationsController(
	IAdmissionCrmOperationsAppService operationsAppService,
	IAdmissionCrmLifecycleAutomationAppService lifecycleAutomationAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/operations/overview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmEnrollmentOverviewDto> GetOverviewAsync(Guid academicYearId) =>
		operationsAppService.GetOverviewAsync(academicYearId);

	[HttpGet("{academicYearId}/operations/queues/summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId) =>
		operationsAppService.GetQueueSummaryAsync(academicYearId);

	[HttpGet("{academicYearId}/operations/queues/{queueType}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmOperationsQueueDto> GetQueueAsync(
		Guid academicYearId,
		AdmissionCrmOperationsQueueType queueType,
		[FromQuery] GetAdmissionCrmOperationsQueueInput input) =>
		operationsAppService.GetQueueAsync(academicYearId, queueType, input);

	[HttpGet("{academicYearId}/operations/saved-views")]
	public Task<IReadOnlyList<AdmissionCrmOperationsSavedViewDto>> GetSavedViewsAsync(Guid academicYearId) =>
		operationsAppService.GetSavedViewsAsync(academicYearId);

	[HttpPost("{academicYearId}/operations/saved-views")]
	public Task<AdmissionCrmOperationsSavedViewDto> CreateSavedViewAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmOperationsSavedViewDto input) =>
		operationsAppService.CreateSavedViewAsync(academicYearId, input);

	[HttpDelete("operations/saved-views/{id}")]
	public Task DeleteSavedViewAsync(Guid id) => operationsAppService.DeleteSavedViewAsync(id);

	[HttpGet("registrations/{registrationApplicationId}/admission-handoff")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public Task<AdmissionCrmAdmissionHandoffPackageDto> GetAdmissionHandoffPackageAsync(
		Guid registrationApplicationId) =>
		operationsAppService.GetAdmissionHandoffPackageAsync(registrationApplicationId);

	[HttpPost("{academicYearId}/automation/lifecycle/run")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public Task<AdmissionCrmLifecycleAutomationResultDto> RunLifecycleAutomationAsync(Guid academicYearId) =>
		lifecycleAutomationAppService.RunAsync(academicYearId);
}
