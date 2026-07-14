using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGuardianRelationship : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid HouseholdId { get; private set; }
	public Guid ApplicantPersonId { get; private set; }
	public Guid GuardianPersonId { get; private set; }
	public AdmissionCrmGuardianRelationshipType RelationshipType { get; private set; }
	public bool IsPrimaryContact { get; private set; }
	public bool IsEmergencyContact { get; private set; }
	public bool IsAccountableContact { get; private set; }
	public bool HasFeeResponsibility { get; private set; }
	public bool HasPickupAuthorization { get; private set; }
	public bool HasPortalAccess { get; private set; }
	public int CommunicationPriority { get; private set; }
	public bool AuthorityVerified { get; private set; }
	public string? AuthorityEvidenceReference { get; private set; }
	public string? ProtectedRestrictionEncrypted { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public bool IsActive { get; private set; }

	protected AdmissionCrmGuardianRelationship()
	{
	}

	public AdmissionCrmGuardianRelationship(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid householdId,
		Guid applicantPersonId, Guid guardianPersonId,
		AdmissionCrmGuardianRelationshipType relationshipType, DateTime effectiveFrom) : base(id)
	{
		if (applicantPersonId == guardianPersonId)
		{
			throw new BusinessException("AdmissionCrm:ApplicantCannotBeOwnGuardian");
		}
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		HouseholdId = householdId;
		ApplicantPersonId = applicantPersonId;
		GuardianPersonId = guardianPersonId;
		RelationshipType = relationshipType;
		EffectiveFrom = effectiveFrom.ToUniversalTime();
		IsActive = true;
	}

	public void ConfigureResponsibilities(
		bool primary, bool emergency, bool accountable, bool feeResponsibility,
		bool pickupAuthorization, bool portalAccess, int communicationPriority)
	{
		if (communicationPriority is < 0 or > 100)
		{
			throw new BusinessException("AdmissionCrm:InvalidGuardianCommunicationPriority");
		}
		IsPrimaryContact = primary;
		IsEmergencyContact = emergency;
		IsAccountableContact = accountable;
		HasFeeResponsibility = feeResponsibility;
		HasPickupAuthorization = pickupAuthorization;
		HasPortalAccess = portalAccess;
		CommunicationPriority = communicationPriority;
	}

	public void VerifyAuthority(string evidenceReference)
	{
		AuthorityEvidenceReference = Check.NotNullOrWhiteSpace(
			evidenceReference, nameof(evidenceReference), AdmissionCrmConsts.MaxFinanceReferenceLength);
		AuthorityVerified = true;
	}

	public void SetProtectedRestriction(string? encryptedRestriction) =>
		ProtectedRestrictionEncrypted = string.IsNullOrWhiteSpace(encryptedRestriction)
			? null
			: Check.Length(encryptedRestriction, nameof(encryptedRestriction), AdmissionCrmConsts.MaxProtectedRestrictionLength);

	public void End(string reason, DateTime at)
	{
		_ = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		var end = at.ToUniversalTime();
		if (end < EffectiveFrom)
		{
			throw new BusinessException("AdmissionCrm:InvalidGuardianRelationshipEnd");
		}
		EffectiveUntil = end;
		IsActive = false;
	}
}
