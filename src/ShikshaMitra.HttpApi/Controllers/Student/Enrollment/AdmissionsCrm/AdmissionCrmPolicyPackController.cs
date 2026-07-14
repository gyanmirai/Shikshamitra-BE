using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmPolicyPackController(IAdmissionCrmPolicyPackAppService appService) : AbpControllerBase
{
	[HttpPost("policy-packs")]
	public Task<AdmissionCrmPolicyPackDto> CreatePolicyPackAsync([FromBody] CreateAdmissionCrmPolicyPackDto input) =>
		appService.CreatePolicyPackAsync(input);

	[HttpGet("policy-packs")]
	public Task<PagedResultDto<AdmissionCrmPolicyPackDto>> GetPolicyPacksAsync([FromQuery] GetAdmissionCrmPolicyPackListInput input) =>
		appService.GetPolicyPacksAsync(input);

	[HttpPost("policy-packs/install-official-baselines")]
	public Task<AdmissionCrmPolicyBaselineInstallResultDto> InstallOfficialBaselineDraftsAsync() =>
		appService.InstallOfficialBaselineDraftsAsync();

	[HttpPost("policy-packs/{id}/submit")]
	public Task<AdmissionCrmPolicyPackDto> SubmitPolicyPackAsync(Guid id) => appService.SubmitPolicyPackAsync(id);

	[HttpPost("policy-packs/{id}/approve")]
	public Task<AdmissionCrmPolicyPackDto> ApprovePolicyPackAsync(Guid id) => appService.ApprovePolicyPackAsync(id);

	[HttpPost("policy-packs/{id}/publish")]
	public Task<AdmissionCrmPolicyPackDto> PublishPolicyPackAsync(Guid id) => appService.PublishPolicyPackAsync(id);

	[HttpPost("{academicYearId}/policy-adoptions/{policyPackId}")]
	public Task<AdmissionCrmPolicyAdoptionDto> AdoptPolicyPackAsync(Guid academicYearId, Guid policyPackId,
		[FromBody] AdoptAdmissionCrmPolicyPackDto input) => appService.AdoptPolicyPackAsync(academicYearId, policyPackId, input);

	[HttpGet("{academicYearId}/policy-adoptions")]
	public Task<IReadOnlyList<AdmissionCrmPolicyAdoptionDto>> GetAdoptionsAsync(Guid academicYearId) =>
		appService.GetAdoptionsAsync(academicYearId);

	[HttpPost("leads/{leadId}/policy-evaluations")]
	public Task<AdmissionCrmPolicyEvaluationDto> EvaluateRuleAsync(Guid leadId,
		[FromBody] EvaluateAdmissionCrmPolicyRuleDto input) => appService.EvaluateRuleAsync(leadId, input);

	[HttpGet("leads/{leadId}/policy-readiness")]
	public Task<AdmissionCrmPolicyReadinessDto> GetPolicyReadinessAsync(Guid leadId) =>
		appService.GetPolicyReadinessAsync(leadId);

	[HttpPost("policy-evaluations/{evaluationId}/exceptions")]
	public Task<AdmissionCrmPolicyExceptionDto> RequestExceptionAsync(Guid evaluationId,
		[FromBody] RequestAdmissionCrmPolicyExceptionDto input) => appService.RequestExceptionAsync(evaluationId, input);

	[HttpPost("policy-exceptions/{id}/decision")]
	public Task<AdmissionCrmPolicyExceptionDto> DecideExceptionAsync(Guid id,
		[FromBody] DecideAdmissionCrmPolicyExceptionDto input) => appService.DecideExceptionAsync(id, input);
}
