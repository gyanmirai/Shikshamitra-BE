using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionEligibilityAppService : IApplicationService
{
	Task<AdmissionEligibilityDto> GetAsync(Guid applicationId, Guid stageId);
	Task<AdmissionEligibilityDto> EvaluateAsync(Guid applicationId, Guid stageId, AdmissionEligibilityEvaluateDto input);
	Task<List<AdmissionEligibilityEvaluationDto>> GetHistoryAsync(Guid applicationId, Guid stageId);
	Task<AdmissionEligibilityDto> RequestExceptionAsync(Guid applicationId, Guid stageId, Guid evaluationId, AdmissionEligibilityExceptionRequestDto input);
	Task<AdmissionEligibilityDto> ReviewExceptionAsync(Guid applicationId, Guid stageId, Guid exceptionId, AdmissionEligibilityExceptionReviewDto input);
	Task<List<AdmissionEligibilityRoleDto>> GetReviewerRolesAsync();
}
