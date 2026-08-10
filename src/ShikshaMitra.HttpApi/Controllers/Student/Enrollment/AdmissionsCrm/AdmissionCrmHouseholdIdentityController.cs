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
	IAdmissionCrmHouseholdProfileService profileService,
	IAdmissionCrmIdentityDirectoryService directoryService,
	IAdmissionCrmIdentityGovernanceService governanceService) : AbpControllerBase
{
	[HttpPost("leads/{leadId}/household-identity")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> CreateProfileAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmApplicantIdentityProfileDto input) =>
		profileService.CreateProfileAsync(leadId, input);

	[HttpGet("leads/{leadId}/household-identity")]
	public Task<AdmissionCrmApplicantIdentityProfileDto?> GetProfileAsync(Guid leadId) =>
		profileService.GetProfileAsync(leadId);

	[HttpGet("household-identity/persons/search")]
	public Task<PagedResultDto<AdmissionCrmPersonDto>> SearchPersonsAsync(
		[FromQuery] SearchAdmissionCrmPersonsInput input) => directoryService.SearchPersonsAsync(input);

	[HttpGet("household-identity/households/search")]
	public Task<PagedResultDto<AdmissionCrmHouseholdSummaryDto>> SearchHouseholdsAsync(
		[FromQuery] SearchAdmissionCrmHouseholdsInput input) => directoryService.SearchHouseholdsAsync(input);

	[HttpPost("leads/{leadId}/household-identity/guardians")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> AddGuardianAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmGuardianRelationshipDto input) =>
		profileService.AddGuardianAsync(leadId, input);

	[HttpPut("household-identity/persons/{personId}/identity")]
	public Task<AdmissionCrmPersonDto> UpdatePersonIdentityAsync(
		Guid personId, [FromBody] UpdateAdmissionCrmPersonIdentityDto input) =>
		directoryService.UpdatePersonIdentityAsync(personId, input);

	[HttpPost("leads/{leadId}/household-identity/consents")]
	public Task<AdmissionCrmIdentityConsentDto> RecordConsentAsync(
		Guid leadId, [FromBody] RecordAdmissionCrmIdentityConsentDto input) =>
		governanceService.RecordConsentAsync(leadId, input);

	[HttpPost("household-identity/guardians/{relationshipId}/verify-authority")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> VerifyGuardianAuthorityAsync(
		Guid relationshipId, [FromBody] VerifyAdmissionCrmGuardianAuthorityDto input) =>
		governanceService.VerifyGuardianAuthorityAsync(relationshipId, input);

	[HttpPut("household-identity/guardians/{relationshipId}/protected-restriction")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> SetGuardianRestrictionAsync(
		Guid relationshipId, [FromBody] SetAdmissionCrmGuardianRestrictionDto input) =>
		governanceService.SetGuardianRestrictionAsync(relationshipId, input);

	[HttpPost("leads/{leadId}/household-identity/override-readiness")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> OverrideReadinessAsync(
		Guid leadId, [FromBody] OverrideAdmissionCrmIdentityReadinessDto input) =>
		governanceService.OverrideReadinessAsync(leadId, input);

	[HttpPost("leads/{leadId}/household-identity/persons/{personId}/link-account")]
	public Task<AdmissionCrmApplicantIdentityProfileDto> LinkPersonAccountAsync(
		Guid leadId, Guid personId, [FromBody] LinkAdmissionCrmPersonAccountDto input) =>
		directoryService.LinkPersonAccountAsync(leadId, personId, input);
}
