using System;
using System.Linq;
using ShikshaMitra.Students.Compliance;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPerson : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? UserId { get; private set; }
	public string FirstName { get; private set; } = string.Empty;
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public DateTime? DateOfBirth { get; private set; }
	public string? Phone { get; private set; }
	public string? NormalizedPhone { get; private set; }
	public string? Email { get; private set; }
	public string? NormalizedEmail { get; private set; }
	public AdmissionCrmPersonProvenance Provenance { get; private set; }
	public bool CoreIdentityVerified { get; private set; }
	public DateTime? CoreIdentityVerifiedAt { get; private set; }
	public string? AadhaarEncrypted { get; private set; }
	public string? AadhaarHash { get; private set; }
	public string? AadhaarLast4 { get; private set; }
	public IdentityVerificationStatus AadhaarStatus { get; private set; }
	public string? PenNumber { get; private set; }
	public IdentityVerificationStatus PenStatus { get; private set; }
	public string? ApaarId { get; private set; }
	public ConsentStatus ApaarConsentStatus { get; private set; }
	public IdentityVerificationStatus ApaarStatus { get; private set; }
	public string? UdiseStudentCode { get; private set; }
	public UdiseSyncStatus UdiseStatus { get; private set; }

	protected AdmissionCrmPerson()
	{
	}

	public AdmissionCrmPerson(
		Guid id, Guid? tenantId, string firstName, string? middleName, string? lastName,
		DateTime? dateOfBirth, string? phone, string? email, AdmissionCrmPersonProvenance provenance) : base(id)
	{
		TenantId = tenantId;
		SetCoreIdentity(firstName, middleName, lastName, dateOfBirth, phone, email, provenance, false, null);
		AadhaarStatus = IdentityVerificationStatus.NotProvided;
		PenStatus = IdentityVerificationStatus.NotProvided;
		ApaarConsentStatus = ConsentStatus.NotAsked;
		ApaarStatus = IdentityVerificationStatus.NotProvided;
		UdiseStatus = UdiseSyncStatus.NotReady;
	}

	public void SetCoreIdentity(
		string firstName, string? middleName, string? lastName, DateTime? dateOfBirth,
		string? phone, string? email, AdmissionCrmPersonProvenance provenance,
		bool verified, DateTime? verifiedAt)
	{
		if (CoreIdentityVerified && !verified && CoreValuesDiffer(firstName, middleName, lastName, dateOfBirth))
		{
			throw new BusinessException("AdmissionCrm:VerifiedPersonIdentityCannotBeOverwritten");
		}
		FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName), AdmissionCrmConsts.MaxNameLength);
		MiddleName = Normalize(middleName, AdmissionCrmConsts.MaxNameLength);
		LastName = Normalize(lastName, AdmissionCrmConsts.MaxNameLength);
		DateOfBirth = dateOfBirth?.Date;
		Phone = Normalize(phone, AdmissionCrmConsts.MaxContactLength);
		NormalizedPhone = NormalizeContact(phone);
		Email = Normalize(email, AdmissionCrmConsts.MaxEmailLength);
		NormalizedEmail = Email?.ToUpperInvariant();
		Provenance = provenance;
		CoreIdentityVerified = verified;
		CoreIdentityVerifiedAt = verified ? (verifiedAt ?? DateTime.UtcNow).ToUniversalTime() : null;
	}

	public void SetAadhaar(
		string? encrypted, string? hash, string? last4, IdentityVerificationStatus status)
	{
		AadhaarEncrypted = Normalize(encrypted, 1000);
		AadhaarHash = Normalize(hash, AdmissionCrmConsts.MaxTokenHashLength);
		AadhaarLast4 = Normalize(last4, 4);
		AadhaarStatus = string.IsNullOrWhiteSpace(encrypted) ? IdentityVerificationStatus.NotProvided : status;
	}

	public void SetGovernmentReadiness(
		string? penNumber, IdentityVerificationStatus penStatus, string? apaarId,
		ConsentStatus apaarConsentStatus, IdentityVerificationStatus apaarStatus,
		string? udiseStudentCode, UdiseSyncStatus udiseStatus)
	{
		PenNumber = NormalizeIdentifier(penNumber, AdmissionCrmConsts.MaxIdentityNumberLength);
		PenStatus = PenNumber == null ? IdentityVerificationStatus.NotProvided : penStatus;
		ApaarId = NormalizeIdentifier(apaarId, AdmissionCrmConsts.MaxIdentityNumberLength);
		ApaarConsentStatus = apaarConsentStatus;
		ApaarStatus = ApaarId == null ? IdentityVerificationStatus.NotProvided : apaarStatus;
		UdiseStudentCode = NormalizeIdentifier(udiseStudentCode, AdmissionCrmConsts.MaxIdentityNumberLength);
		UdiseStatus = udiseStatus;
	}

	public void LinkStudent(Guid studentId) => StudentId = studentId;
	public void LinkUser(Guid userId) => UserId = userId;

	private bool CoreValuesDiffer(string firstName, string? middleName, string? lastName, DateTime? dateOfBirth) =>
		!string.Equals(FirstName, firstName.Trim(), StringComparison.OrdinalIgnoreCase) ||
		!string.Equals(MiddleName, middleName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
		!string.Equals(LastName, lastName?.Trim(), StringComparison.OrdinalIgnoreCase) || DateOfBirth != dateOfBirth?.Date;

	private static string? Normalize(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), maxLength);
	private static string? NormalizeIdentifier(string? value, int maxLength) => Normalize(value, maxLength)?.ToUpperInvariant();
	private static string? NormalizeContact(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());
}
