using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmHouseholdIdentityAppService : IApplicationService
{
	Task<AdmissionCrmApplicantIdentityProfileDto> CreateProfileAsync(Guid leadId, CreateAdmissionCrmApplicantIdentityProfileDto input);
	Task<AdmissionCrmApplicantIdentityProfileDto?> GetProfileAsync(Guid leadId);
	Task<PagedResultDto<AdmissionCrmPersonDto>> SearchPersonsAsync(SearchAdmissionCrmPersonsInput input);
	Task<PagedResultDto<AdmissionCrmHouseholdSummaryDto>> SearchHouseholdsAsync(SearchAdmissionCrmHouseholdsInput input);
	Task<AdmissionCrmApplicantIdentityProfileDto> AddGuardianAsync(Guid leadId, CreateAdmissionCrmGuardianRelationshipDto input);
	Task<AdmissionCrmPersonDto> UpdatePersonIdentityAsync(Guid personId, UpdateAdmissionCrmPersonIdentityDto input);
	Task<AdmissionCrmIdentityConsentDto> RecordConsentAsync(Guid leadId, RecordAdmissionCrmIdentityConsentDto input);
	Task<AdmissionCrmApplicantIdentityProfileDto> VerifyGuardianAuthorityAsync(Guid relationshipId, VerifyAdmissionCrmGuardianAuthorityDto input);
	Task<AdmissionCrmApplicantIdentityProfileDto> SetGuardianRestrictionAsync(Guid relationshipId, SetAdmissionCrmGuardianRestrictionDto input);
	Task<AdmissionCrmApplicantIdentityProfileDto> OverrideReadinessAsync(Guid leadId, OverrideAdmissionCrmIdentityReadinessDto input);
	Task<AdmissionCrmApplicantIdentityProfileDto> LinkPersonAccountAsync(Guid leadId, Guid personId, LinkAdmissionCrmPersonAccountDto input);
}
