using System;
using System.Collections.Generic;
using ShikshaMitra.Students.Compliance;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAddressDto
{
	public string Type { get; set; } = "Correspondence";
	public string Line1 { get; set; } = string.Empty;
	public string? Line2 { get; set; }
	public string? Locality { get; set; }
	public string City { get; set; } = string.Empty;
	public string State { get; set; } = string.Empty;
	public string PostalCode { get; set; } = string.Empty;
	public string Country { get; set; } = "India";
	public bool IsVerified { get; set; }
}

public class AdmissionCrmPersonInputDto
{
	public string FirstName { get; set; } = string.Empty;
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public string? Phone { get; set; }
	public string? Email { get; set; }
	public AdmissionCrmPersonProvenance Provenance { get; set; } = AdmissionCrmPersonProvenance.StaffSupplied;
}

public class AdmissionCrmPersonDto : EntityDto<Guid>
{
	public Guid? StudentId { get; set; }
	public Guid? UserId { get; set; }
	public string FirstName { get; set; } = string.Empty;
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public string? Phone { get; set; }
	public string? Email { get; set; }
	public AdmissionCrmPersonProvenance Provenance { get; set; }
	public bool CoreIdentityVerified { get; set; }
	public DateTime? CoreIdentityVerifiedAt { get; set; }
	public string? AadhaarMasked { get; set; }
	public IdentityVerificationStatus AadhaarStatus { get; set; }
	public string? PenNumber { get; set; }
	public IdentityVerificationStatus PenStatus { get; set; }
	public string? ApaarId { get; set; }
	public ConsentStatus ApaarConsentStatus { get; set; }
	public IdentityVerificationStatus ApaarStatus { get; set; }
	public string? UdiseStudentCode { get; set; }
	public UdiseSyncStatus UdiseStatus { get; set; }
}

public class CreateAdmissionCrmApplicantIdentityProfileDto
{
	public Guid? ExistingHouseholdId { get; set; }
	public Guid? ExistingApplicantPersonId { get; set; }
	public string HouseholdDisplayName { get; set; } = string.Empty;
	public string PreferredLanguage { get; set; } = "en";
	public IReadOnlyList<AdmissionCrmAddressDto> Addresses { get; set; } = [];
	public IReadOnlyDictionary<string, string?> CommunicationPreferences { get; set; } =
		new Dictionary<string, string?>();
	public AdmissionCrmPersonInputDto Applicant { get; set; } = new();
}

public class CreateAdmissionCrmGuardianRelationshipDto
{
	public Guid? ExistingGuardianPersonId { get; set; }
	public AdmissionCrmPersonInputDto Guardian { get; set; } = new();
	public AdmissionCrmGuardianRelationshipType RelationshipType { get; set; }
	public bool IsPrimaryContact { get; set; }
	public bool IsEmergencyContact { get; set; }
	public bool IsAccountableContact { get; set; }
	public bool HasFeeResponsibility { get; set; }
	public bool HasPickupAuthorization { get; set; }
	public bool HasPortalAccess { get; set; }
	public int CommunicationPriority { get; set; }
}

public class AdmissionCrmGuardianRelationshipDto : EntityDto<Guid>
{
	public Guid GuardianPersonId { get; set; }
	public AdmissionCrmPersonDto Guardian { get; set; } = new();
	public AdmissionCrmGuardianRelationshipType RelationshipType { get; set; }
	public bool IsPrimaryContact { get; set; }
	public bool IsEmergencyContact { get; set; }
	public bool IsAccountableContact { get; set; }
	public bool HasFeeResponsibility { get; set; }
	public bool HasPickupAuthorization { get; set; }
	public bool HasPortalAccess { get; set; }
	public int CommunicationPriority { get; set; }
	public bool AuthorityVerified { get; set; }
	public string? AuthorityEvidenceReference { get; set; }
	public bool HasProtectedRestriction { get; set; }
	public string? ProtectedRestriction { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public bool IsActive { get; set; }
}

public class AdmissionCrmApplicantIdentityProfileDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid ApplicantPersonId { get; set; }
	public Guid HouseholdId { get; set; }
	public string HouseholdCode { get; set; } = string.Empty;
	public string HouseholdDisplayName { get; set; } = string.Empty;
	public string PreferredLanguage { get; set; } = "en";
	public IReadOnlyList<AdmissionCrmAddressDto> Addresses { get; set; } = [];
	public AdmissionCrmPersonDto Applicant { get; set; } = new();
	public IReadOnlyList<AdmissionCrmGuardianRelationshipDto> Guardians { get; set; } = [];
	public AdmissionCrmIdentityResolutionStatus Status { get; set; }
	public bool AccountableContactReady { get; set; }
	public bool IdentityConsentReady { get; set; }
	public bool AccountLinkReady { get; set; }
	public string? ConflictReason { get; set; }
	public bool OverrideUsed { get; set; }
	public string? OverrideReason { get; set; }
}

public class UpdateAdmissionCrmPersonIdentityDto
{
	public string? AadhaarNo { get; set; }
	public IdentityVerificationStatus AadhaarStatus { get; set; }
	public string? PenNumber { get; set; }
	public IdentityVerificationStatus PenStatus { get; set; }
	public string? ApaarId { get; set; }
	public ConsentStatus ApaarConsentStatus { get; set; }
	public IdentityVerificationStatus ApaarStatus { get; set; }
	public string? UdiseStudentCode { get; set; }
	public UdiseSyncStatus UdiseStatus { get; set; }
}

public class RecordAdmissionCrmIdentityConsentDto
{
	public Guid PersonId { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public string TextVersion { get; set; } = string.Empty;
	public string Language { get; set; } = "en";
	public string Actor { get; set; } = string.Empty;
	public string Channel { get; set; } = string.Empty;
	public bool IsGranted { get; set; }
}

public class AdmissionCrmIdentityConsentDto : EntityDto<Guid>
{
	public Guid PersonId { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public string TextVersion { get; set; } = string.Empty;
	public string Language { get; set; } = "en";
	public string Actor { get; set; } = string.Empty;
	public string Channel { get; set; } = string.Empty;
	public bool IsGranted { get; set; }
	public DateTime RecordedAt { get; set; }
	public DateTime? WithdrawnAt { get; set; }
	public string? WithdrawalReason { get; set; }
}

public class SetAdmissionCrmGuardianRestrictionDto
{
	public string? Restriction { get; set; }
}

public class VerifyAdmissionCrmGuardianAuthorityDto
{
	public string EvidenceReference { get; set; } = string.Empty;
}

public class OverrideAdmissionCrmIdentityReadinessDto
{
	public string Reason { get; set; } = string.Empty;
}

public class LinkAdmissionCrmPersonAccountDto
{
	public Guid UserId { get; set; }
}

public class SearchAdmissionCrmPersonsInput : PagedAndSortedResultRequestDto
{
	public string? Name { get; set; }
	public string? Contact { get; set; }
	public DateTime? DateOfBirth { get; set; }
}

public class SearchAdmissionCrmHouseholdsInput : PagedAndSortedResultRequestDto
{
	public string? SearchText { get; set; }
}

public class AdmissionCrmHouseholdSummaryDto : EntityDto<Guid>
{
	public string Code { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string PreferredLanguage { get; set; } = "en";
	public int ActiveMemberCount { get; set; }
	public IReadOnlyList<AdmissionCrmAddressDto> Addresses { get; set; } = [];
}
