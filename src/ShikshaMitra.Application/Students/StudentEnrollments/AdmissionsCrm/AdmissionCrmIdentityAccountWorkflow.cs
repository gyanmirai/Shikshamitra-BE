using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityAccountWorkflow(
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> profileRepository,
	IRepository<AdmissionCrmPerson, Guid> personRepository,
	AdmissionCrmIdentityEntityStore entityStore,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmIdentityReadinessWorkflow readinessWorkflow,
	AdmissionCrmIdentityProfileMapper profileMapper,
	IIdentityUserRepository identityUserRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmApplicantIdentityProfileDto> LinkAsync(
		Guid leadId,
		Guid personId,
		LinkAdmissionCrmPersonAccountDto input,
		string operation)
	{
		await leadAccessReader.GetAsync(leadId, operation);
		var profileQueryable = await profileRepository.GetQueryableAsync();
		var profile = await AsyncExecuter.FirstOrDefaultAsync(profileQueryable.Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == leadId)) ??
			throw new BusinessException("AdmissionCrm:ApplicantIdentityProfileNotFound");
		var linkedPersonIds = (await entityStore.GetGuardiansAsync(leadId))
			.Select(relationship => relationship.GuardianPersonId)
			.Append(profile.ApplicantPersonId).ToHashSet();
		if (!linkedPersonIds.Contains(personId))
			throw new BusinessException("AdmissionCrm:PersonNotLinkedToApplicant");
		var person = await entityStore.GetPersonAsync(personId);
		if (await identityUserRepository.FindAsync(input.UserId) == null)
			throw new BusinessException("AdmissionCrm:AccountUserNotFound");
		var personQueryable = await personRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(personQueryable.Where(item =>
			item.TenantId == CurrentTenant.Id && item.Id != personId && item.UserId == input.UserId)))
			throw new BusinessException("AdmissionCrm:AccountAlreadyLinkedToAnotherPerson");
		person.LinkUser(input.UserId);
		await personRepository.UpdateAsync(person, autoSave: true);
		await readinessWorkflow.RecomputeAsync(profile);
		return await profileMapper.MapAsync(profile);
	}
}
