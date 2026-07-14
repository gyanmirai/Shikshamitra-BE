using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmPolicyPackAppService : IApplicationService
{
	Task<AdmissionCrmPolicyPackDto> CreatePolicyPackAsync(CreateAdmissionCrmPolicyPackDto input);
	Task<PagedResultDto<AdmissionCrmPolicyPackDto>> GetPolicyPacksAsync(GetAdmissionCrmPolicyPackListInput input);
	Task<AdmissionCrmPolicyBaselineInstallResultDto> InstallOfficialBaselineDraftsAsync();
	Task<AdmissionCrmPolicyPackDto> SubmitPolicyPackAsync(Guid id);
	Task<AdmissionCrmPolicyPackDto> ApprovePolicyPackAsync(Guid id);
	Task<AdmissionCrmPolicyPackDto> PublishPolicyPackAsync(Guid id);
	Task<AdmissionCrmPolicyAdoptionDto> AdoptPolicyPackAsync(Guid academicYearId, Guid policyPackId, AdoptAdmissionCrmPolicyPackDto input);
	Task<IReadOnlyList<AdmissionCrmPolicyAdoptionDto>> GetAdoptionsAsync(Guid academicYearId);
	Task<AdmissionCrmPolicyEvaluationDto> EvaluateRuleAsync(Guid leadId, EvaluateAdmissionCrmPolicyRuleDto input);
	Task<AdmissionCrmPolicyReadinessDto> GetPolicyReadinessAsync(Guid leadId);
	Task<AdmissionCrmPolicyExceptionDto> RequestExceptionAsync(Guid evaluationId, RequestAdmissionCrmPolicyExceptionDto input);
	Task<AdmissionCrmPolicyExceptionDto> DecideExceptionAsync(Guid id, DecideAdmissionCrmPolicyExceptionDto input);
}
