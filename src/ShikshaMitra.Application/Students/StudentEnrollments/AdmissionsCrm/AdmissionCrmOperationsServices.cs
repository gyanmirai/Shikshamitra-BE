using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
public class AdmissionCrmOperationsOverviewService(AdmissionCrmOperationsQueueWorkflow workflow) : ApplicationService, IAdmissionCrmOperationsOverviewService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmEnrollmentOverviewDto> GetOverviewAsync(Guid academicYearId) => workflow.GetOverviewAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId) => workflow.GetQueueSummaryAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmOperationsQueueDto> GetQueueAsync(Guid academicYearId, AdmissionCrmOperationsQueueType queueType, GetAdmissionCrmOperationsQueueInput input) => workflow.GetQueueAsync(academicYearId, queueType, input);
}

[Authorize]
public class AdmissionCrmMyWorkService(AdmissionCrmMyWorkWorkflow workflow) : ApplicationService, IAdmissionCrmMyWorkService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmMyWorkDto> GetMyWorkAsync(Guid academicYearId, GetAdmissionCrmMyWorkInput input) => workflow.GetMyWorkAsync(academicYearId, input);
}

[Authorize]
public class AdmissionCrmOperationsSavedViewService(AdmissionCrmOperationsSavedViewWorkflow workflow) : ApplicationService, IAdmissionCrmOperationsSavedViewService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public Task<AdmissionCrmOperationsSavedViewDto> CreateSavedViewAsync(Guid academicYearId, CreateAdmissionCrmOperationsSavedViewDto input) => workflow.CreateSavedViewAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<IReadOnlyList<AdmissionCrmOperationsSavedViewDto>> GetSavedViewsAsync(Guid academicYearId) => workflow.GetSavedViewsAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOperationsQueues)]
	public Task DeleteSavedViewAsync(Guid id) => workflow.DeleteSavedViewAsync(id);
}

[Authorize]
public class AdmissionCrmAdmissionHandoffService(AdmissionCrmAdmissionHandoffWorkflow workflow) : ApplicationService, IAdmissionCrmAdmissionHandoffService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public Task<AdmissionCrmAdmissionHandoffPackageDto> GetAdmissionHandoffPackageAsync(Guid registrationApplicationId) => workflow.GetAdmissionHandoffPackageAsync(registrationApplicationId);
}