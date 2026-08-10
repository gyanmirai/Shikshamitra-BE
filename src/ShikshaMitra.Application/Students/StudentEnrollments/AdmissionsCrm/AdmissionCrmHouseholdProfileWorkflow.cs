using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmHouseholdProfileWorkflow(
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> profileRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository,
	AdmissionCrmIdentityEntityStore entityStore,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmIdentityReadinessWorkflow readinessWorkflow,
	AdmissionCrmIdentityProfileMapper profileMapper) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmApplicantIdentityProfileDto> CreateAsync(
		Guid leadId,
		CreateAdmissionCrmApplicantIdentityProfileDto input,
		string operation)
	{
		var lead = await leadAccessReader.GetAsync(leadId, operation);
		if (await FindProfileAsync(leadId) != null)
			throw new BusinessException("AdmissionCrm:ApplicantIdentityProfileAlreadyExists");
		var household = input.ExistingHouseholdId.HasValue
			? await entityStore.GetHouseholdAsync(input.ExistingHouseholdId.Value)
			: await entityStore.CreateHouseholdAsync(input);
		var applicant = input.ExistingApplicantPersonId.HasValue
			? await entityStore.GetPersonAsync(input.ExistingApplicantPersonId.Value)
			: await entityStore.CreatePersonAsync(AdmissionCrmIdentityMapper.ResolveApplicant(input.Applicant, lead));
		await entityStore.EnsureMembershipAsync(household.Id, applicant.Id, AdmissionCrmHouseholdRole.Applicant);
		var profile = new AdmissionCrmApplicantIdentityProfile(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, applicant.Id, household.Id);
		await profileRepository.InsertAsync(profile, autoSave: true);
		await readinessWorkflow.RecomputeAsync(profile);
		return await profileMapper.MapAsync(profile);
	}

	public async Task<AdmissionCrmApplicantIdentityProfileDto?> GetAsync(Guid leadId, string operation)
	{
		await leadAccessReader.GetAsync(leadId, operation);
		var profile = await FindProfileAsync(leadId);
		return profile == null ? null : await profileMapper.MapAsync(profile);
	}

	public async Task<AdmissionCrmApplicantIdentityProfileDto> AddGuardianAsync(
		Guid leadId,
		CreateAdmissionCrmGuardianRelationshipDto input,
		string operation)
	{
		var profile = await GetProfileAsync(leadId, operation);
		var guardian = input.ExistingGuardianPersonId.HasValue
			? await entityStore.GetPersonAsync(input.ExistingGuardianPersonId.Value)
			: await entityStore.CreatePersonAsync(input.Guardian ??
				throw new BusinessException("AdmissionCrm:GuardianDetailsRequired"));
		await entityStore.EnsureMembershipAsync(profile.HouseholdId, guardian.Id, AdmissionCrmHouseholdRole.Guardian);
		var existingQueryable = await guardianRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(existingQueryable.Where(relationship =>
			relationship.TenantId == CurrentTenant.Id && relationship.LeadId == leadId &&
			relationship.GuardianPersonId == guardian.Id && relationship.IsActive)))
			throw new BusinessException("AdmissionCrm:GuardianRelationshipAlreadyExists");
		var relationship = new AdmissionCrmGuardianRelationship(
			GuidGenerator.Create(), CurrentTenant.Id, profile.AcademicYearId, leadId, profile.HouseholdId,
			profile.ApplicantPersonId, guardian.Id, input.RelationshipType, Clock.Now);
		relationship.ConfigureResponsibilities(
			input.IsPrimaryContact, input.IsEmergencyContact, input.IsAccountableContact,
			input.HasFeeResponsibility, input.HasPickupAuthorization, input.HasPortalAccess,
			input.CommunicationPriority);
		await guardianRepository.InsertAsync(relationship, autoSave: true);
		await readinessWorkflow.RecomputeAsync(profile);
		return await profileMapper.MapAsync(profile);
	}

	public async Task<AdmissionCrmApplicantIdentityProfile> GetProfileAsync(Guid leadId, string operation)
	{
		await leadAccessReader.GetAsync(leadId, operation);
		return await FindProfileAsync(leadId) ??
			throw new BusinessException("AdmissionCrm:ApplicantIdentityProfileNotFound");
	}

	private async Task<AdmissionCrmApplicantIdentityProfile?> FindProfileAsync(Guid leadId)
	{
		var queryable = await profileRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(profile =>
			profile.TenantId == CurrentTenant.Id && profile.LeadId == leadId));
	}
}
