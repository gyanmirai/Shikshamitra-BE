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
public class AdmissionCrmPolicyPackController(
	IAdmissionCrmPolicyCatalogService catalogService,
	IAdmissionCrmPolicyAdoptionService adoptionService,
	IAdmissionCrmPolicyEvaluationService evaluationService) : AbpControllerBase
{
	[HttpPost("policy-packs")]
	public Task<AdmissionCrmPolicyPackDto> CreatePolicyPackAsync([FromBody] CreateAdmissionCrmPolicyPackDto input) =>
		catalogService.CreatePolicyPackAsync(input);

	[HttpGet("policy-packs")]
	public Task<PagedResultDto<AdmissionCrmPolicyPackDto>> GetPolicyPacksAsync([FromQuery] GetAdmissionCrmPolicyPackListInput input) =>
		catalogService.GetPolicyPacksAsync(input);

	[HttpPost("policy-packs/install-official-baselines")]
	public Task<AdmissionCrmPolicyBaselineInstallResultDto> InstallOfficialBaselineDraftsAsync() =>
		catalogService.InstallOfficialBaselineDraftsAsync();

	[HttpPost("policy-packs/{id}/submit")]
	public Task<AdmissionCrmPolicyPackDto> SubmitPolicyPackAsync(Guid id) => catalogService.SubmitPolicyPackAsync(id);

	[HttpPost("policy-packs/{id}/approve")]
	public Task<AdmissionCrmPolicyPackDto> ApprovePolicyPackAsync(Guid id) => catalogService.ApprovePolicyPackAsync(id);

	[HttpPost("policy-packs/{id}/publish")]
	public Task<AdmissionCrmPolicyPackDto> PublishPolicyPackAsync(Guid id) => catalogService.PublishPolicyPackAsync(id);

	[HttpPost("{academicYearId}/policy-adoptions/{policyPackId}")]
	public Task<AdmissionCrmPolicyAdoptionDto> AdoptPolicyPackAsync(Guid academicYearId, Guid policyPackId,
		[FromBody] AdoptAdmissionCrmPolicyPackDto input) => adoptionService.AdoptPolicyPackAsync(academicYearId, policyPackId, input);

	[HttpGet("{academicYearId}/policy-adoptions")]
	public Task<IReadOnlyList<AdmissionCrmPolicyAdoptionDto>> GetAdoptionsAsync(Guid academicYearId) =>
		adoptionService.GetAdoptionsAsync(academicYearId);

	[HttpPost("leads/{leadId}/policy-evaluations")]
	public Task<AdmissionCrmPolicyEvaluationDto> EvaluateRuleAsync(Guid leadId,
		[FromBody] EvaluateAdmissionCrmPolicyRuleDto input) => evaluationService.EvaluateRuleAsync(leadId, input);

	[HttpGet("leads/{leadId}/policy-readiness")]
	public Task<AdmissionCrmPolicyReadinessDto> GetPolicyReadinessAsync(Guid leadId) =>
		evaluationService.GetPolicyReadinessAsync(leadId);

	[HttpPost("policy-evaluations/{evaluationId}/exceptions")]
	public Task<AdmissionCrmPolicyExceptionDto> RequestExceptionAsync(Guid evaluationId,
		[FromBody] RequestAdmissionCrmPolicyExceptionDto input) => evaluationService.RequestExceptionAsync(evaluationId, input);

	[HttpPost("policy-exceptions/{id}/decision")]
	public Task<AdmissionCrmPolicyExceptionDto> DecideExceptionAsync(Guid id,
		[FromBody] DecideAdmissionCrmPolicyExceptionDto input) => evaluationService.DecideExceptionAsync(id, input);
}
