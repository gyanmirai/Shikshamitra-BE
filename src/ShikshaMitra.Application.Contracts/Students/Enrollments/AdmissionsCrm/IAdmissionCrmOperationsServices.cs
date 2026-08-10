using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmOperationsOverviewService : IApplicationService
{
	Task<AdmissionCrmEnrollmentOverviewDto> GetOverviewAsync(Guid academicYearId);
	Task<AdmissionCrmOperationsQueueSummaryDto> GetQueueSummaryAsync(Guid academicYearId);
	Task<AdmissionCrmOperationsQueueDto> GetQueueAsync(Guid academicYearId, AdmissionCrmOperationsQueueType queueType, GetAdmissionCrmOperationsQueueInput input);
}

public interface IAdmissionCrmMyWorkService : IApplicationService
{
	Task<AdmissionCrmMyWorkDto> GetMyWorkAsync(Guid academicYearId, GetAdmissionCrmMyWorkInput input);
}

public interface IAdmissionCrmOperationsSavedViewService : IApplicationService
{
	Task<AdmissionCrmOperationsSavedViewDto> CreateSavedViewAsync(Guid academicYearId, CreateAdmissionCrmOperationsSavedViewDto input);
	Task<IReadOnlyList<AdmissionCrmOperationsSavedViewDto>> GetSavedViewsAsync(Guid academicYearId);
	Task DeleteSavedViewAsync(Guid id);
}

public interface IAdmissionCrmAdmissionHandoffService : IApplicationService
{
	Task<AdmissionCrmAdmissionHandoffPackageDto> GetAdmissionHandoffPackageAsync(Guid registrationApplicationId);
}