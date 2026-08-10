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
	IAdmissionCrmOperationsOverviewService operationsOverviewService,
	IAdmissionCrmMyWorkService myWorkService,
	IAdmissionCrmOperationsSavedViewService savedViewService,
	IAdmissionCrmAdmissionHandoffService handoffService,
	IAdmissionCrmLifecycleAutomationAppService lifecycleAutomationAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/operations/overview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmEnrollmentOverviewDto> GetOverviewAsync(Guid academicYearId) =>
		operationsOverviewService.GetOverviewAsync(academicYearId);

	[HttpGet("{academicYearId}/operations/my-work")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmMyWorkDto> GetMyWorkAsync(
		Guid academicYearId,
		[FromQuery] GetAdmissionCrmMyWorkInput input) =>
		myWorkService.GetMyWorkAsync(academicYearId, input);

	[HttpGet("{academicYearId}/operations/queues/summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId) =>
		operationsOverviewService.GetQueueSummaryAsync(academicYearId);

	[HttpGet("{academicYearId}/operations/queues/{queueType}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmOperationsQueueDto> GetQueueAsync(
		Guid academicYearId,
		AdmissionCrmOperationsQueueType queueType,
		[FromQuery] GetAdmissionCrmOperationsQueueInput input) =>
		operationsOverviewService.GetQueueAsync(academicYearId, queueType, input);

	[HttpGet("{academicYearId}/operations/saved-views")]
	public Task<IReadOnlyList<AdmissionCrmOperationsSavedViewDto>> GetSavedViewsAsync(Guid academicYearId) =>
		savedViewService.GetSavedViewsAsync(academicYearId);

	[HttpPost("{academicYearId}/operations/saved-views")]
	public Task<AdmissionCrmOperationsSavedViewDto> CreateSavedViewAsync(Guid academicYearId, [FromBody] CreateAdmissionCrmOperationsSavedViewDto input) =>
		savedViewService.CreateSavedViewAsync(academicYearId, input);

	[HttpDelete("operations/saved-views/{id}")]
	public Task DeleteSavedViewAsync(Guid id) => savedViewService.DeleteSavedViewAsync(id);

	[HttpGet("registrations/{registrationApplicationId}/admission-handoff")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public Task<AdmissionCrmAdmissionHandoffPackageDto> GetAdmissionHandoffPackageAsync(
		Guid registrationApplicationId) =>
		handoffService.GetAdmissionHandoffPackageAsync(registrationApplicationId);

	[HttpPost("{academicYearId}/automation/lifecycle/run")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public Task<AdmissionCrmLifecycleAutomationResultDto> RunLifecycleAutomationAsync(Guid academicYearId) =>
		lifecycleAutomationAppService.RunAsync(academicYearId);
}