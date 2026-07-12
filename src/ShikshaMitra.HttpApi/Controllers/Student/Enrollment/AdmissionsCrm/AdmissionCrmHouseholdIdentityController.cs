using System;
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
public class AdmissionCrmHouseholdIdentityController(
	IAdmissionCrmHouseholdIdentityAppService appService) : AbpControllerBase
{
	[HttpPost("leads/{leadId}/household-identity")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> CreateProfileAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmApplicantIdentityProfileDto input) =>
		appService.CreateProfileAsync(leadId, input);

	[HttpGet("leads/{leadId}/household-identity")]
	public Task<AdmissionCrmApplicantIdentityProfileDto?> GetProfileAsync(Guid leadId) =>
		appService.GetProfileAsync(leadId);

	[HttpGet("household-identity/persons/search")]
	public Task<PagedResultDto<AdmissionCrmPersonDto>> SearchPersonsAsync(
		[FromQuery] SearchAdmissionCrmPersonsInput input) => appService.SearchPersonsAsync(input);

	[HttpGet("household-identity/households/search")]
	public Task<PagedResultDto<AdmissionCrmHouseholdSummaryDto>> SearchHouseholdsAsync(
		[FromQuery] SearchAdmissionCrmHouseholdsInput input) => appService.SearchHouseholdsAsync(input);

	[HttpPost("leads/{leadId}/household-identity/guardians")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> AddGuardianAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmGuardianRelationshipDto input) =>
		appService.AddGuardianAsync(leadId, input);

	[HttpPut("household-identity/persons/{personId}/identity")]
	public Task<AdmissionCrmPersonDto> UpdatePersonIdentityAsync(
		Guid personId, [FromBody] UpdateAdmissionCrmPersonIdentityDto input) =>
		appService.UpdatePersonIdentityAsync(personId, input);

	[HttpPost("leads/{leadId}/household-identity/consents")]
	public Task<AdmissionCrmIdentityConsentDto> RecordConsentAsync(
		Guid leadId, [FromBody] RecordAdmissionCrmIdentityConsentDto input) =>
		appService.RecordConsentAsync(leadId, input);

	[HttpPost("household-identity/guardians/{relationshipId}/verify-authority")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> VerifyGuardianAuthorityAsync(
		Guid relationshipId, [FromBody] VerifyAdmissionCrmGuardianAuthorityDto input) =>
		appService.VerifyGuardianAuthorityAsync(relationshipId, input);

	[HttpPut("household-identity/guardians/{relationshipId}/protected-restriction")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> SetGuardianRestrictionAsync(
		Guid relationshipId, [FromBody] SetAdmissionCrmGuardianRestrictionDto input) =>
		appService.SetGuardianRestrictionAsync(relationshipId, input);

	[HttpPost("leads/{leadId}/household-identity/override-readiness")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> OverrideReadinessAsync(
		Guid leadId, [FromBody] OverrideAdmissionCrmIdentityReadinessDto input) =>
		appService.OverrideReadinessAsync(leadId, input);

	[HttpPost("leads/{leadId}/household-identity/persons/{personId}/link-account")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> LinkPersonAccountAsync(
		Guid leadId, Guid personId, [FromBody] LinkAdmissionCrmPersonAccountDto input) =>
		appService.LinkPersonAccountAsync(leadId, personId, input);
}
