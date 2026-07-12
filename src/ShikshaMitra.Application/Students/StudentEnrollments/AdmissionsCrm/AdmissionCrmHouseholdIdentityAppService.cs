using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Compliance;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmHouseholdIdentityAppService : ApplicationService, IAdmissionCrmHouseholdIdentityAppService
{
	private const string IdentityProcessingPurpose = "Identity processing";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmPerson, Guid> _personRepository;
	private readonly IRepository<AdmissionCrmHousehold, Guid> _householdRepository;
	private readonly IRepository<AdmissionCrmHouseholdMembership, Guid> _membershipRepository;
	private readonly IRepository<AdmissionCrmApplicantIdentityProfile, Guid> _profileRepository;
	private readonly IRepository<AdmissionCrmGuardianRelationship, Guid> _guardianRepository;
	private readonly IRepository<AdmissionCrmIdentityConsent, Guid> _consentRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly IStringEncryptionService _encryptionService;
	private readonly IIdentityUserRepository _identityUserRepository;

	public AdmissionCrmHouseholdIdentityAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmPerson, Guid> personRepository,
		IRepository<AdmissionCrmHousehold, Guid> householdRepository,
		IRepository<AdmissionCrmHouseholdMembership, Guid> membershipRepository,
		IRepository<AdmissionCrmApplicantIdentityProfile, Guid> profileRepository,
		IRepository<AdmissionCrmGuardianRelationship, Guid> guardianRepository,
		IRepository<AdmissionCrmIdentityConsent, Guid> consentRepository,
		StudentScopeAccessService studentScopeAccessService,
		IStringEncryptionService encryptionService,
		IIdentityUserRepository identityUserRepository)
	{
		_leadRepository = leadRepository;
		_personRepository = personRepository;
		_householdRepository = householdRepository;
		_membershipRepository = membershipRepository;
		_profileRepository = profileRepository;
		_guardianRepository = guardianRepository;
		_consentRepository = consentRepository;
		_studentScopeAccessService = studentScopeAccessService;
		_encryptionService = encryptionService;
		_identityUserRepository = identityUserRepository;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageHouseholds)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto> CreateProfileAsync(
		Guid leadId, CreateAdmissionCrmApplicantIdentityProfileDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(CreateProfileAsync));
		if (await FindProfileAsync(leadId) != null)
		{
			throw new BusinessException("AdmissionCrm:ApplicantIdentityProfileAlreadyExists");
		}

		var household = input.ExistingHouseholdId.HasValue
			? await GetHouseholdAsync(input.ExistingHouseholdId.Value)
			: await CreateHouseholdAsync(input);
		var applicant = input.ExistingApplicantPersonId.HasValue
			? await GetPersonAsync(input.ExistingApplicantPersonId.Value)
			: await CreatePersonAsync(ResolveApplicantInput(input.Applicant, lead));
		await EnsureMembershipAsync(household.Id, applicant.Id, AdmissionCrmHouseholdRole.Applicant);
		var profile = new AdmissionCrmApplicantIdentityProfile(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, applicant.Id, household.Id);
		await _profileRepository.InsertAsync(profile, autoSave: true);
		await RecomputeReadinessAsync(profile);
		return await MapProfileAsync(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto?> GetProfileAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetProfileAsync));
		var profile = await FindProfileAsync(leadId);
		return profile == null ? null : await MapProfileAsync(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds)]
	public async Task<PagedResultDto<AdmissionCrmPersonDto>> SearchPersonsAsync(SearchAdmissionCrmPersonsInput input)
	{
		var queryable = await _personRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id));
		var contact = NormalizeContact(input.Contact);
		var name = input.Name?.Trim();
		var filtered = entities
			.Where(x => string.IsNullOrWhiteSpace(name) || FullName(x).Contains(name, StringComparison.OrdinalIgnoreCase))
			.Where(x => string.IsNullOrWhiteSpace(contact) || x.NormalizedPhone == contact ||
				x.NormalizedEmail == input.Contact?.Trim().ToUpperInvariant())
			.Where(x => !input.DateOfBirth.HasValue || x.DateOfBirth == input.DateOfBirth.Value.Date)
			.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToList();
		return new PagedResultDto<AdmissionCrmPersonDto>(filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(MapPerson).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds)]
	public async Task<PagedResultDto<AdmissionCrmHouseholdSummaryDto>> SearchHouseholdsAsync(
		SearchAdmissionCrmHouseholdsInput input)
	{
		var queryable = await _householdRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.IsActive));
		var searchText = input.SearchText?.Trim();
		var filtered = entities.Where(x => string.IsNullOrWhiteSpace(searchText) ||
			x.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			x.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.DisplayName).ToList();
		var membershipQueryable = await _membershipRepository.GetQueryableAsync();
		var memberships = await AsyncExecuter.ToListAsync(membershipQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.IsActive));
		return new PagedResultDto<AdmissionCrmHouseholdSummaryDto>(filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(x => new AdmissionCrmHouseholdSummaryDto
			{
				Id = x.Id,
				Code = x.Code,
				DisplayName = x.DisplayName,
				PreferredLanguage = x.PreferredLanguage,
				ActiveMemberCount = memberships.Count(y => y.HouseholdId == x.Id),
				Addresses = Deserialize<IReadOnlyList<AdmissionCrmAddressDto>>(x.AddressesJson) ?? []
			}).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGuardianAuthority)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto> AddGuardianAsync(
		Guid leadId, CreateAdmissionCrmGuardianRelationshipDto input)
	{
		var profile = await GetProfileEntityAsync(leadId, nameof(AddGuardianAsync));
		var guardian = input.ExistingGuardianPersonId.HasValue
			? await GetPersonAsync(input.ExistingGuardianPersonId.Value)
			: await CreatePersonAsync(input.Guardian);
		await EnsureMembershipAsync(profile.HouseholdId, guardian.Id, AdmissionCrmHouseholdRole.Guardian);
		var existingQueryable = await _guardianRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(existingQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			x.GuardianPersonId == guardian.Id && x.IsActive)))
		{
			throw new BusinessException("AdmissionCrm:GuardianRelationshipAlreadyExists");
		}
		var relationship = new AdmissionCrmGuardianRelationship(
			GuidGenerator.Create(), CurrentTenant.Id, profile.AcademicYearId, leadId, profile.HouseholdId,
			profile.ApplicantPersonId, guardian.Id, input.RelationshipType, Clock.Now);
		relationship.ConfigureResponsibilities(
			input.IsPrimaryContact, input.IsEmergencyContact, input.IsAccountableContact,
			input.HasFeeResponsibility, input.HasPickupAuthorization, input.HasPortalAccess,
			input.CommunicationPriority);
		await _guardianRepository.InsertAsync(relationship, autoSave: true);
		await RecomputeReadinessAsync(profile);
		return await MapProfileAsync(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGovernmentReadiness)]
	public async Task<AdmissionCrmPersonDto> UpdatePersonIdentityAsync(
		Guid personId, UpdateAdmissionCrmPersonIdentityDto input)
	{
		var person = await GetPersonAsync(personId);
		var aadhaar = NormalizeDigits(input.AadhaarNo);
		if (!string.IsNullOrWhiteSpace(aadhaar) && aadhaar.Length != 12)
		{
			throw new BusinessException("AdmissionCrm:AadhaarInvalid");
		}
		if (!string.IsNullOrWhiteSpace(aadhaar))
		{
			var hash = Sha256(aadhaar);
			var queryable = await _personRepository.GetQueryableAsync();
			if (await AsyncExecuter.AnyAsync(queryable.Where(x =>
				x.TenantId == CurrentTenant.Id && x.Id != personId && x.AadhaarHash == hash)))
			{
				throw new BusinessException("AdmissionCrm:AadhaarAlreadyLinkedToAnotherPerson");
			}
			person.SetAadhaar(_encryptionService.Encrypt(aadhaar), hash, aadhaar[^4..], input.AadhaarStatus);
		}
		person.SetGovernmentReadiness(
			input.PenNumber, input.PenStatus, input.ApaarId, input.ApaarConsentStatus,
			input.ApaarStatus, input.UdiseStudentCode, input.UdiseStatus);
		await _personRepository.UpdateAsync(person, autoSave: true);
		await RecomputeProfilesForPersonAsync(personId);
		return MapPerson(person);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageHouseholds)]
	public async Task<AdmissionCrmIdentityConsentDto> RecordConsentAsync(
		Guid leadId, RecordAdmissionCrmIdentityConsentDto input)
	{
		var profile = await GetProfileEntityAsync(leadId, nameof(RecordConsentAsync));
		if (input.PersonId != profile.ApplicantPersonId)
		{
			var guardians = await GetGuardianEntitiesAsync(leadId);
			if (guardians.All(x => x.GuardianPersonId != input.PersonId))
			{
				throw new BusinessException("AdmissionCrm:ConsentPersonNotLinkedToApplicant");
			}
		}
		var consent = new AdmissionCrmIdentityConsent(
			GuidGenerator.Create(), CurrentTenant.Id, profile.AcademicYearId, leadId, input.PersonId,
			input.Purpose, input.TextVersion, input.Language, input.Actor, input.Channel,
			input.IsGranted, Clock.Now);
		await _consentRepository.InsertAsync(consent, autoSave: true);
		await RecomputeReadinessAsync(profile);
		return MapConsent(consent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGuardianAuthority)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto> VerifyGuardianAuthorityAsync(
		Guid relationshipId, VerifyAdmissionCrmGuardianAuthorityDto input)
	{
		var relationship = await GetGuardianAsync(relationshipId);
		relationship.VerifyAuthority(input.EvidenceReference);
		await _guardianRepository.UpdateAsync(relationship, autoSave: true);
		var profile = await GetProfileEntityAsync(relationship.LeadId, nameof(VerifyGuardianAuthorityAsync));
		await RecomputeReadinessAsync(profile);
		return await MapProfileAsync(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageProtectedRelationships)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto> SetGuardianRestrictionAsync(
		Guid relationshipId, SetAdmissionCrmGuardianRestrictionDto input)
	{
		var relationship = await GetGuardianAsync(relationshipId);
		relationship.SetProtectedRestriction(string.IsNullOrWhiteSpace(input.Restriction)
			? null
			: _encryptionService.Encrypt(input.Restriction.Trim()));
		await _guardianRepository.UpdateAsync(relationship, autoSave: true);
		var profile = await GetProfileEntityAsync(relationship.LeadId, nameof(SetGuardianRestrictionAsync));
		return await MapProfileAsync(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideIdentityReadiness)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto> OverrideReadinessAsync(
		Guid leadId, OverrideAdmissionCrmIdentityReadinessDto input)
	{
		var profile = await GetProfileEntityAsync(leadId, nameof(OverrideReadinessAsync));
		profile.Override(input.Reason, GetCurrentUserId(), Clock.Now);
		await _profileRepository.UpdateAsync(profile, autoSave: true);
		return await MapProfileAsync(profile);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.LinkAccounts)]
	public async Task<AdmissionCrmApplicantIdentityProfileDto> LinkPersonAccountAsync(
		Guid leadId, Guid personId, LinkAdmissionCrmPersonAccountDto input)
	{
		var profile = await GetProfileEntityAsync(leadId, nameof(LinkPersonAccountAsync));
		var linkedPersonIds = (await GetGuardianEntitiesAsync(leadId)).Select(x => x.GuardianPersonId)
			.Append(profile.ApplicantPersonId).ToHashSet();
		if (!linkedPersonIds.Contains(personId))
		{
			throw new BusinessException("AdmissionCrm:PersonNotLinkedToApplicant");
		}
		var person = await GetPersonAsync(personId);
		if (await _identityUserRepository.FindAsync(input.UserId) == null)
		{
			throw new BusinessException("AdmissionCrm:AccountUserNotFound");
		}
		var personQueryable = await _personRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(personQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.Id != personId && x.UserId == input.UserId)))
		{
			throw new BusinessException("AdmissionCrm:AccountAlreadyLinkedToAnotherPerson");
		}
		person.LinkUser(input.UserId);
		await _personRepository.UpdateAsync(person, autoSave: true);
		await RecomputeReadinessAsync(profile);
		return await MapProfileAsync(profile);
	}

	private async Task RecomputeReadinessAsync(AdmissionCrmApplicantIdentityProfile profile)
	{
		var guardians = await GetGuardianEntitiesAsync(profile.LeadId);
		var accountableReady = guardians.Any(x => x.IsActive && x.IsAccountableContact && x.AuthorityVerified);
		var consentQueryable = await _consentRepository.GetQueryableAsync();
		var consents = await AsyncExecuter.ToListAsync(consentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == profile.LeadId &&
			x.PersonId == profile.ApplicantPersonId && x.Purpose == IdentityProcessingPurpose));
		var identityConsentReady = consents.OrderByDescending(x => x.RecordedAt).FirstOrDefault()?.IsGranted == true;
		var portalGuardianIds = guardians.Where(x => x.IsActive && x.HasPortalAccess)
			.Select(x => x.GuardianPersonId).Distinct().ToList();
		var accountLinkReady = portalGuardianIds.Count == 0;
		if (portalGuardianIds.Count > 0)
		{
			var personQueryable = await _personRepository.GetQueryableAsync();
			var people = await AsyncExecuter.ToListAsync(personQueryable.Where(x =>
				x.TenantId == CurrentTenant.Id && portalGuardianIds.Contains(x.Id)));
			accountLinkReady = people.Count == portalGuardianIds.Count && people.All(x => x.UserId.HasValue);
		}
		profile.Evaluate(accountableReady, identityConsentReady, accountLinkReady, null);
		await _profileRepository.UpdateAsync(profile, autoSave: true);
	}

	private async Task RecomputeProfilesForPersonAsync(Guid personId)
	{
		var profileQueryable = await _profileRepository.GetQueryableAsync();
		var profiles = await AsyncExecuter.ToListAsync(profileQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ApplicantPersonId == personId));
		var guardianQueryable = await _guardianRepository.GetQueryableAsync();
		var guardianLeadIds = (await AsyncExecuter.ToListAsync(guardianQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.GuardianPersonId == personId && x.IsActive)))
			.Select(x => x.LeadId).ToHashSet();
		if (guardianLeadIds.Count > 0)
		{
			profiles.AddRange(await AsyncExecuter.ToListAsync(profileQueryable.Where(x =>
				x.TenantId == CurrentTenant.Id && guardianLeadIds.Contains(x.LeadId))));
		}
		foreach (var profile in profiles.DistinctBy(x => x.Id))
		{
			await RecomputeReadinessAsync(profile);
		}
	}

	private async Task<AdmissionCrmApplicantIdentityProfileDto> MapProfileAsync(AdmissionCrmApplicantIdentityProfile profile)
	{
		var household = await GetHouseholdAsync(profile.HouseholdId);
		var applicant = await GetPersonAsync(profile.ApplicantPersonId);
		var relationships = await GetGuardianEntitiesAsync(profile.LeadId);
		var canViewProtected = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewProtectedRelationships);
		var guardians = new List<AdmissionCrmGuardianRelationshipDto>();
		foreach (var relationship in relationships.OrderBy(x => x.CommunicationPriority))
		{
			var guardian = await GetPersonAsync(relationship.GuardianPersonId);
			guardians.Add(new AdmissionCrmGuardianRelationshipDto
			{
				Id = relationship.Id,
				GuardianPersonId = guardian.Id,
				Guardian = MapPerson(guardian),
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
					? _encryptionService.Decrypt(relationship.ProtectedRestrictionEncrypted)
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
			Addresses = Deserialize<IReadOnlyList<AdmissionCrmAddressDto>>(household.AddressesJson) ?? [],
			Applicant = MapPerson(applicant),
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

	private async Task<AdmissionCrmHousehold> CreateHouseholdAsync(CreateAdmissionCrmApplicantIdentityProfileDto input)
	{
		var household = new AdmissionCrmHousehold(
			GuidGenerator.Create(), CurrentTenant.Id, $"HH-{GuidGenerator.Create():N}",
			input.HouseholdDisplayName, input.PreferredLanguage,
			JsonSerializer.Serialize(input.Addresses, JsonOptions),
			JsonSerializer.Serialize(input.CommunicationPreferences, JsonOptions));
		return await _householdRepository.InsertAsync(household, autoSave: true);
	}

	private async Task<AdmissionCrmPerson> CreatePersonAsync(AdmissionCrmPersonInputDto input)
	{
		if (string.IsNullOrWhiteSpace(input.FirstName))
		{
			throw new BusinessException("AdmissionCrm:PersonFirstNameRequired");
		}
		var normalizedPhone = NormalizeContact(input.Phone);
		var normalizedEmail = input.Email?.Trim().ToUpperInvariant();
		var queryable = await _personRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			((normalizedPhone != null && x.NormalizedPhone == normalizedPhone) ||
			 (normalizedEmail != null && x.NormalizedEmail == normalizedEmail))));
		var likelyDuplicate = candidates.FirstOrDefault(x =>
			x.FirstName.Equals(input.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
			(!input.DateOfBirth.HasValue || x.DateOfBirth == input.DateOfBirth.Value.Date));
		if (likelyDuplicate != null)
		{
			throw new BusinessException("AdmissionCrm:PossiblePersonDuplicate")
				.WithData("ExistingPersonId", likelyDuplicate.Id);
		}
		var person = new AdmissionCrmPerson(
			GuidGenerator.Create(), CurrentTenant.Id, input.FirstName, input.MiddleName, input.LastName,
			input.DateOfBirth, input.Phone, input.Email, input.Provenance);
		return await _personRepository.InsertAsync(person, autoSave: true);
	}

	private async Task EnsureMembershipAsync(Guid householdId, Guid personId, AdmissionCrmHouseholdRole role)
	{
		var queryable = await _membershipRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.HouseholdId == householdId && x.PersonId == personId && x.IsActive)))
		{
			return;
		}
		await _membershipRepository.InsertAsync(new AdmissionCrmHouseholdMembership(
			GuidGenerator.Create(), CurrentTenant.Id, householdId, personId, role, Clock.Now), autoSave: true);
	}

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.FindAsync(id);
		if (lead == null || lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation);
		return lead;
	}

	private async Task<AdmissionCrmApplicantIdentityProfile> GetProfileEntityAsync(Guid leadId, string operation)
	{
		await GetLeadAsync(leadId, operation);
		return await FindProfileAsync(leadId) ?? throw new BusinessException("AdmissionCrm:ApplicantIdentityProfileNotFound");
	}

	private async Task<AdmissionCrmApplicantIdentityProfile?> FindProfileAsync(Guid leadId)
	{
		var queryable = await _profileRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	private async Task<AdmissionCrmPerson> GetPersonAsync(Guid id)
	{
		var person = await _personRepository.FindAsync(id);
		return person != null && person.TenantId == CurrentTenant.Id
			? person
			: throw new BusinessException("AdmissionCrm:PersonNotFound");
	}

	private async Task<AdmissionCrmHousehold> GetHouseholdAsync(Guid id)
	{
		var household = await _householdRepository.FindAsync(id);
		return household != null && household.TenantId == CurrentTenant.Id
			? household
			: throw new BusinessException("AdmissionCrm:HouseholdNotFound");
	}

	private async Task<AdmissionCrmGuardianRelationship> GetGuardianAsync(Guid id)
	{
		var relationship = await _guardianRepository.FindAsync(id);
		return relationship != null && relationship.TenantId == CurrentTenant.Id
			? relationship
			: throw new BusinessException("AdmissionCrm:GuardianRelationshipNotFound");
	}

	private async Task<List<AdmissionCrmGuardianRelationship>> GetGuardianEntitiesAsync(Guid leadId)
	{
		var queryable = await _guardianRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	private static AdmissionCrmPersonInputDto ResolveApplicantInput(
		AdmissionCrmPersonInputDto input, AdmissionInquiryLead lead) => new()
	{
		FirstName = string.IsNullOrWhiteSpace(input.FirstName) ? lead.FirstName ?? "Applicant" : input.FirstName,
		MiddleName = input.MiddleName ?? lead.MiddleName,
		LastName = input.LastName ?? lead.LastName,
		DateOfBirth = input.DateOfBirth ?? lead.DateOfBirth?.UtcDateTime,
		Phone = input.Phone ?? lead.PrimaryContactNo,
		Email = input.Email ?? lead.Email,
		Provenance = input.Provenance
	};

	private static AdmissionCrmPersonDto MapPerson(AdmissionCrmPerson person) => new()
	{
		Id = person.Id,
		StudentId = person.StudentId,
		UserId = person.UserId,
		FirstName = person.FirstName,
		MiddleName = person.MiddleName,
		LastName = person.LastName,
		DateOfBirth = person.DateOfBirth,
		Phone = person.Phone,
		Email = person.Email,
		Provenance = person.Provenance,
		CoreIdentityVerified = person.CoreIdentityVerified,
		CoreIdentityVerifiedAt = person.CoreIdentityVerifiedAt,
		AadhaarMasked = string.IsNullOrWhiteSpace(person.AadhaarLast4) ? null : $"XXXX-XXXX-{person.AadhaarLast4}",
		AadhaarStatus = person.AadhaarStatus,
		PenNumber = person.PenNumber,
		PenStatus = person.PenStatus,
		ApaarId = person.ApaarId,
		ApaarConsentStatus = person.ApaarConsentStatus,
		ApaarStatus = person.ApaarStatus,
		UdiseStudentCode = person.UdiseStudentCode,
		UdiseStatus = person.UdiseStatus
	};

	private static AdmissionCrmIdentityConsentDto MapConsent(AdmissionCrmIdentityConsent entity) => new()
	{
		Id = entity.Id, PersonId = entity.PersonId, Purpose = entity.Purpose,
		TextVersion = entity.TextVersion, Language = entity.Language, Actor = entity.Actor,
		Channel = entity.Channel, IsGranted = entity.IsGranted, RecordedAt = entity.RecordedAt,
		WithdrawnAt = entity.WithdrawnAt, WithdrawalReason = entity.WithdrawalReason
	};

	private Guid GetCurrentUserId() => CurrentUser.Id ?? throw new BusinessException("AdmissionCrm:AuthenticatedUserRequired");
	private static string FullName(AdmissionCrmPerson person) => string.Join(" ",
		new[] { person.FirstName, person.MiddleName, person.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
	private static string? NormalizeContact(string? value) => string.IsNullOrWhiteSpace(value)
		? null : new string(value.Where(char.IsDigit).ToArray());
	private static string? NormalizeDigits(string? value) => NormalizeContact(value);
	private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	private static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
}
