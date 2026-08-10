using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityReadinessWorkflow(
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> profileRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository,
	IRepository<AdmissionCrmIdentityConsent, Guid> consentRepository,
	IRepository<AdmissionCrmPerson, Guid> personRepository) : ApplicationService, ITransientDependency
{
	private const string IdentityProcessingPurpose = "Identity processing";

	public async Task RecomputeAsync(AdmissionCrmApplicantIdentityProfile profile)
	{
		var guardianQueryable = await guardianRepository.GetQueryableAsync();
		var guardians = await AsyncExecuter.ToListAsync(guardianQueryable.Where(relationship =>
			relationship.TenantId == CurrentTenant.Id && relationship.LeadId == profile.LeadId));
		var accountableReady = guardians.Any(relationship =>
			relationship.IsActive && relationship.IsAccountableContact && relationship.AuthorityVerified);
		var consentQueryable = await consentRepository.GetQueryableAsync();
		var consents = await AsyncExecuter.ToListAsync(consentQueryable.Where(consent =>
			consent.TenantId == CurrentTenant.Id && consent.LeadId == profile.LeadId &&
			consent.PersonId == profile.ApplicantPersonId && consent.Purpose == IdentityProcessingPurpose));
		var identityConsentReady = consents.OrderByDescending(consent => consent.RecordedAt).FirstOrDefault()?.IsGranted == true;
		var portalGuardianIds = guardians.Where(relationship => relationship.IsActive && relationship.HasPortalAccess)
			.Select(relationship => relationship.GuardianPersonId).Distinct().ToList();
		var accountLinkReady = portalGuardianIds.Count == 0;
		if (portalGuardianIds.Count > 0)
		{
			var personQueryable = await personRepository.GetQueryableAsync();
			var people = await AsyncExecuter.ToListAsync(personQueryable.Where(person =>
				person.TenantId == CurrentTenant.Id && portalGuardianIds.Contains(person.Id)));
			accountLinkReady = people.Count == portalGuardianIds.Count && people.All(person => person.UserId.HasValue);
		}
		profile.Evaluate(accountableReady, identityConsentReady, accountLinkReady, null);
		await profileRepository.UpdateAsync(profile, autoSave: true);
	}

	public async Task RecomputeForPersonAsync(Guid personId)
	{
		var profileQueryable = await profileRepository.GetQueryableAsync();
		var profiles = await AsyncExecuter.ToListAsync(profileQueryable.Where(profile =>
			profile.TenantId == CurrentTenant.Id && profile.ApplicantPersonId == personId));
		var guardianQueryable = await guardianRepository.GetQueryableAsync();
		var guardianLeadIds = (await AsyncExecuter.ToListAsync(guardianQueryable.Where(relationship =>
			relationship.TenantId == CurrentTenant.Id && relationship.GuardianPersonId == personId && relationship.IsActive)))
			.Select(relationship => relationship.LeadId).ToHashSet();
		if (guardianLeadIds.Count > 0)
		{
			profiles.AddRange(await AsyncExecuter.ToListAsync(profileQueryable.Where(profile =>
				profile.TenantId == CurrentTenant.Id && guardianLeadIds.Contains(profile.LeadId))));
		}
		foreach (var profile in profiles.DistinctBy(item => item.Id))
			await RecomputeAsync(profile);
	}
}
