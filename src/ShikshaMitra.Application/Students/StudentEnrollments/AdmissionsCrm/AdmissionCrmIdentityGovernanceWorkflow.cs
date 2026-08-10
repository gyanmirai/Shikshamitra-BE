using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityGovernanceWorkflow(
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> profileRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository,
	IRepository<AdmissionCrmIdentityConsent, Guid> consentRepository,
	AdmissionCrmIdentityEntityStore entityStore,
	AdmissionCrmHouseholdProfileWorkflow profileWorkflow,
	AdmissionCrmIdentityReadinessWorkflow readinessWorkflow,
	AdmissionCrmIdentityProfileMapper profileMapper,
	IStringEncryptionService encryptionService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmIdentityConsentDto> RecordConsentAsync(
		Guid leadId,
		RecordAdmissionCrmIdentityConsentDto input,
		string operation)
	{
		var profile = await profileWorkflow.GetProfileAsync(leadId, operation);
		if (input.PersonId != profile.ApplicantPersonId)
		{
			var guardians = await entityStore.GetGuardiansAsync(leadId);
			if (guardians.All(relationship => relationship.GuardianPersonId != input.PersonId))
				throw new BusinessException("AdmissionCrm:ConsentPersonNotLinkedToApplicant");
		}
		var consent = new AdmissionCrmIdentityConsent(
			GuidGenerator.Create(), CurrentTenant.Id, profile.AcademicYearId, leadId, input.PersonId,
			input.Purpose, input.TextVersion, input.Language, input.Actor, input.Channel,
			input.IsGranted, Clock.Now);
		await consentRepository.InsertAsync(consent, autoSave: true);
		await readinessWorkflow.RecomputeAsync(profile);
		return AdmissionCrmIdentityMapper.Map(consent);
	}

	public async Task<AdmissionCrmApplicantIdentityProfileDto> VerifyGuardianAsync(
		Guid relationshipId,
		VerifyAdmissionCrmGuardianAuthorityDto input,
		string operation)
	{
		var relationship = await entityStore.GetGuardianAsync(relationshipId);
		relationship.VerifyAuthority(input.EvidenceReference);
		await guardianRepository.UpdateAsync(relationship, autoSave: true);
		var profile = await profileWorkflow.GetProfileAsync(relationship.LeadId, operation);
		await readinessWorkflow.RecomputeAsync(profile);
		return await profileMapper.MapAsync(profile);
	}

	public async Task<AdmissionCrmApplicantIdentityProfileDto> SetRestrictionAsync(
		Guid relationshipId,
		SetAdmissionCrmGuardianRestrictionDto input,
		string operation)
	{
		var relationship = await entityStore.GetGuardianAsync(relationshipId);
		relationship.SetProtectedRestriction(string.IsNullOrWhiteSpace(input.Restriction)
			? null
			: encryptionService.Encrypt(input.Restriction.Trim()));
		await guardianRepository.UpdateAsync(relationship, autoSave: true);
		var profile = await profileWorkflow.GetProfileAsync(relationship.LeadId, operation);
		return await profileMapper.MapAsync(profile);
	}

	public async Task<AdmissionCrmApplicantIdentityProfileDto> OverrideReadinessAsync(
		Guid leadId,
		OverrideAdmissionCrmIdentityReadinessDto input,
		string operation)
	{
		var profile = await profileWorkflow.GetProfileAsync(leadId, operation);
		profile.Override(input.Reason, GetCurrentUserId(), Clock.Now);
		await profileRepository.UpdateAsync(profile, autoSave: true);
		return await profileMapper.MapAsync(profile);
	}

	private Guid GetCurrentUserId() =>
		CurrentUser.Id ?? throw new BusinessException("AdmissionCrm:AuthenticatedUserRequired");
}
