using System;
using ShikshaMitra.Students.Compliance;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Compliance;

public class StudentIdentityCompliance : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public string? AadhaarEncrypted { get; private set; }
	public string? AadhaarHash { get; private set; }
	public string? AadhaarLast4 { get; private set; }
	public string? AadhaarName { get; private set; }
	public IdentityVerificationStatus AadhaarVerificationStatus { get; private set; }
	public string? ApaarId { get; private set; }
	public ConsentStatus ApaarConsentStatus { get; private set; }
	public DateTimeOffset? ApaarConsentDate { get; private set; }
	public IdentityVerificationStatus ApaarVerificationStatus { get; private set; }
	public string? PenNumber { get; private set; }
	public string? UdiseStudentCode { get; private set; }
	public UdiseSyncStatus UdiseSyncStatus { get; private set; }
	public DateTimeOffset? UdiseLastExportedAt { get; private set; }
	public bool RteStatus { get; private set; }
	public ScholarshipStatus ScholarshipStatus { get; private set; }
	public string? ScholarshipSchemeName { get; private set; }
	public string? ScholarshipApplicationNo { get; private set; }
	public string? CasteCertificateNo { get; private set; }
	public IdentityVerificationStatus CasteCertificateVerifiedStatus { get; private set; }

	private StudentIdentityCompliance()
	{
	}

	public StudentIdentityCompliance(Guid id, Guid? tenantId, Guid studentId) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		AadhaarVerificationStatus = IdentityVerificationStatus.NotProvided;
		ApaarConsentStatus = ConsentStatus.NotAsked;
		ApaarVerificationStatus = IdentityVerificationStatus.NotProvided;
		UdiseSyncStatus = UdiseSyncStatus.NotReady;
		ScholarshipStatus = ScholarshipStatus.NotApplicable;
		CasteCertificateVerifiedStatus = IdentityVerificationStatus.NotProvided;
	}

	public void SetAadhaar(
		string? aadhaarEncrypted,
		string? aadhaarHash,
		string? aadhaarLast4,
		string? aadhaarName,
		IdentityVerificationStatus status)
	{
		AadhaarEncrypted = string.IsNullOrWhiteSpace(aadhaarEncrypted) ? null : aadhaarEncrypted;
		AadhaarHash = string.IsNullOrWhiteSpace(aadhaarHash) ? null : aadhaarHash;
		AadhaarLast4 = string.IsNullOrWhiteSpace(aadhaarLast4) ? null : Check.Length(aadhaarLast4, nameof(aadhaarLast4), 4);
		AadhaarName = string.IsNullOrWhiteSpace(aadhaarName) ? null : Check.Length(aadhaarName.Trim(), nameof(aadhaarName), 200);
		AadhaarVerificationStatus = string.IsNullOrWhiteSpace(aadhaarEncrypted)
			? IdentityVerificationStatus.NotProvided
			: status;
	}

	public void SetAadhaarVerificationStatus(IdentityVerificationStatus status)
	{
		AadhaarVerificationStatus = status;
	}

	public void SetApaar(string? apaarId, ConsentStatus consentStatus, DateTimeOffset? consentDate, IdentityVerificationStatus verificationStatus)
	{
		ApaarId = string.IsNullOrWhiteSpace(apaarId) ? null : Check.Length(apaarId.Trim(), nameof(apaarId), 20);
		ApaarConsentStatus = consentStatus;
		ApaarConsentDate = consentStatus is ConsentStatus.Granted or ConsentStatus.Refused or ConsentStatus.Revoked
			? consentDate ?? DateTimeOffset.UtcNow
			: consentDate;
		ApaarVerificationStatus = string.IsNullOrWhiteSpace(apaarId)
			? IdentityVerificationStatus.NotProvided
			: verificationStatus;
	}

	public void SetApaarConsent(ConsentStatus consentStatus, DateTimeOffset? consentDate)
	{
		ApaarConsentStatus = consentStatus;
		ApaarConsentDate = consentStatus is ConsentStatus.Granted or ConsentStatus.Refused or ConsentStatus.Revoked
			? consentDate ?? DateTimeOffset.UtcNow
			: consentDate;
	}

	public void SetAdministrativeIdentifiers(string? penNumber, string? udiseStudentCode, UdiseSyncStatus udiseStatus)
	{
		PenNumber = string.IsNullOrWhiteSpace(penNumber) ? null : Check.Length(penNumber.Trim(), nameof(penNumber), 20);
		UdiseStudentCode = string.IsNullOrWhiteSpace(udiseStudentCode) ? null : Check.Length(udiseStudentCode.Trim(), nameof(udiseStudentCode), 50);
		UdiseSyncStatus = udiseStatus;
	}

	public void MarkUdiseExported(DateTimeOffset exportedAt)
	{
		UdiseSyncStatus = UdiseSyncStatus.Exported;
		UdiseLastExportedAt = exportedAt;
	}

	public void SetBenefitTracking(
		bool rteStatus,
		ScholarshipStatus scholarshipStatus,
		string? scholarshipSchemeName,
		string? scholarshipApplicationNo,
		string? casteCertificateNo,
		IdentityVerificationStatus casteCertificateStatus)
	{
		RteStatus = rteStatus;
		ScholarshipStatus = scholarshipStatus;
		ScholarshipSchemeName = string.IsNullOrWhiteSpace(scholarshipSchemeName) ? null : Check.Length(scholarshipSchemeName.Trim(), nameof(scholarshipSchemeName), 200);
		ScholarshipApplicationNo = string.IsNullOrWhiteSpace(scholarshipApplicationNo) ? null : Check.Length(scholarshipApplicationNo.Trim(), nameof(scholarshipApplicationNo), 50);
		CasteCertificateNo = string.IsNullOrWhiteSpace(casteCertificateNo) ? null : Check.Length(casteCertificateNo.Trim(), nameof(casteCertificateNo), 50);
		CasteCertificateVerifiedStatus = casteCertificateStatus;
	}
}
