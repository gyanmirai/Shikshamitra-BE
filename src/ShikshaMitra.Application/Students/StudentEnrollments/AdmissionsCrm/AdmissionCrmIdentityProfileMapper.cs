using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityProfileMapper(
	AdmissionCrmIdentityEntityStore entityStore,
	IStringEncryptionService encryptionService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmApplicantIdentityProfileDto> MapAsync(AdmissionCrmApplicantIdentityProfile profile)
	{
		var household = await entityStore.GetHouseholdAsync(profile.HouseholdId);
		var applicant = await entityStore.GetPersonAsync(profile.ApplicantPersonId);
		var relationships = await entityStore.GetGuardiansAsync(profile.LeadId);
		var canViewProtected = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewProtectedRelationships);
		var guardians = new List<AdmissionCrmGuardianRelationshipDto>();
		foreach (var relationship in relationships.OrderBy(item => item.CommunicationPriority))
		{
			var guardian = await entityStore.GetPersonAsync(relationship.GuardianPersonId);
			guardians.Add(new AdmissionCrmGuardianRelationshipDto
			{
				Id = relationship.Id,
				GuardianPersonId = guardian.Id,
				Guardian = AdmissionCrmIdentityMapper.Map(guardian),
				RelationshipType = relationship.RelationshipType,
				IsPrimaryContact = relationship.IsPrimaryContact,
				IsEmergencyContact = relationship.IsEmergencyContact,
				IsAccountableContact = relationship.IsAccountableContact,
				HasFeeResponsibility = relationship.HasFeeResponsibility,
				HasPickupAuthorization = relationship.HasPickupAuthorization,
				HasPortalAccess = relationship.HasPortalAccess,
				CommunicationPriority = relationship.CommunicationPriority,
				AuthorityVerified = relationship.AuthorityVerified,
				AuthorityEvidenceReference = relationship.AuthorityEvidenceReference,
				HasProtectedRestriction = relationship.ProtectedRestrictionEncrypted != null,
				ProtectedRestriction = canViewProtected && relationship.ProtectedRestrictionEncrypted != null
					? encryptionService.Decrypt(relationship.ProtectedRestrictionEncrypted)
					: null,
				EffectiveFrom = relationship.EffectiveFrom,
				EffectiveUntil = relationship.EffectiveUntil,
				IsActive = relationship.IsActive
			});
		}

		return new AdmissionCrmApplicantIdentityProfileDto
		{
			Id = profile.Id,
			AcademicYearId = profile.AcademicYearId,
			LeadId = profile.LeadId,
			ApplicantPersonId = profile.ApplicantPersonId,
			HouseholdId = profile.HouseholdId,
			HouseholdCode = household.Code,
			HouseholdDisplayName = household.DisplayName,
			PreferredLanguage = household.PreferredLanguage,
			Addresses = AdmissionCrmIdentityMapper.Deserialize<IReadOnlyList<AdmissionCrmAddressDto>>(household.AddressesJson) ?? [],
			Applicant = AdmissionCrmIdentityMapper.Map(applicant),
			Guardians = guardians,
			Status = profile.Status,
			AccountableContactReady = profile.AccountableContactReady,
			IdentityConsentReady = profile.IdentityConsentReady,
			AccountLinkReady = profile.AccountLinkReady,
			ConflictReason = profile.ConflictReason,
			OverrideUsed = profile.OverrideUsed,
			OverrideReason = profile.OverrideReason
		};
	}
}
