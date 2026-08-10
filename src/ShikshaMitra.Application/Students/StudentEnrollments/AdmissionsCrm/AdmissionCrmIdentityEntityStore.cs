using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityEntityStore(
	IRepository<AdmissionCrmPerson, Guid> personRepository,
	IRepository<AdmissionCrmHousehold, Guid> householdRepository,
	IRepository<AdmissionCrmHouseholdMembership, Guid> membershipRepository,
	IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmHousehold> CreateHouseholdAsync(CreateAdmissionCrmApplicantIdentityProfileDto input)
	{
		var household = new AdmissionCrmHousehold(
			GuidGenerator.Create(), CurrentTenant.Id, $"HH-{GuidGenerator.Create():N}",
			input.HouseholdDisplayName, input.PreferredLanguage,
			AdmissionCrmIdentityMapper.Serialize(input.Addresses),
			AdmissionCrmIdentityMapper.Serialize(input.CommunicationPreferences));
		return await householdRepository.InsertAsync(household, autoSave: true);
	}

	public async Task<AdmissionCrmPerson> CreatePersonAsync(AdmissionCrmPersonInputDto input)
	{
		if (string.IsNullOrWhiteSpace(input.FirstName))
			throw new BusinessException("AdmissionCrm:PersonFirstNameRequired");
		var normalizedPhone = AdmissionCrmIdentityMapper.NormalizeContact(input.Phone);
		var normalizedEmail = input.Email?.Trim().ToUpperInvariant();
		var queryable = await personRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(queryable.Where(person =>
			person.TenantId == CurrentTenant.Id &&
			((normalizedPhone != null && person.NormalizedPhone == normalizedPhone) ||
			 (normalizedEmail != null && person.NormalizedEmail == normalizedEmail))));
		var likelyDuplicate = candidates.FirstOrDefault(person =>
			person.FirstName.Equals(input.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
			(!input.DateOfBirth.HasValue || person.DateOfBirth == input.DateOfBirth.Value.Date));
		if (likelyDuplicate != null)
			throw new BusinessException("AdmissionCrm:PossiblePersonDuplicate")
				.WithData("ExistingPersonId", likelyDuplicate.Id);
		var person = new AdmissionCrmPerson(
			GuidGenerator.Create(), CurrentTenant.Id, input.FirstName, input.MiddleName, input.LastName,
			input.DateOfBirth, input.Phone, input.Email, input.Provenance);
		return await personRepository.InsertAsync(person, autoSave: true);
	}

	public async Task EnsureMembershipAsync(Guid householdId, Guid personId, AdmissionCrmHouseholdRole role)
	{
		var queryable = await membershipRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(membership =>
			membership.TenantId == CurrentTenant.Id && membership.HouseholdId == householdId &&
			membership.PersonId == personId && membership.IsActive)))
			return;
		await membershipRepository.InsertAsync(new AdmissionCrmHouseholdMembership(
			GuidGenerator.Create(), CurrentTenant.Id, householdId, personId, role, Clock.Now), autoSave: true);
	}

	public async Task<AdmissionCrmPerson> GetPersonAsync(Guid id)
	{
		var person = await personRepository.FindAsync(id);
		return person != null && person.TenantId == CurrentTenant.Id
			? person
			: throw new BusinessException("AdmissionCrm:PersonNotFound");
	}

	public async Task<AdmissionCrmHousehold> GetHouseholdAsync(Guid id)
	{
		var household = await householdRepository.FindAsync(id);
		return household != null && household.TenantId == CurrentTenant.Id
			? household
			: throw new BusinessException("AdmissionCrm:HouseholdNotFound");
	}

	public async Task<AdmissionCrmGuardianRelationship> GetGuardianAsync(Guid id)
	{
		var relationship = await guardianRepository.FindAsync(id);
		return relationship != null && relationship.TenantId == CurrentTenant.Id
			? relationship
			: throw new BusinessException("AdmissionCrm:GuardianRelationshipNotFound");
	}

	public async Task<List<AdmissionCrmGuardianRelationship>> GetGuardiansAsync(Guid leadId)
	{
		var queryable = await guardianRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(queryable.Where(relationship =>
			relationship.TenantId == CurrentTenant.Id && relationship.LeadId == leadId));
	}
}
