using System;
using ShikshaMitra.Students.Compliance;

namespace ShikshaMitra.Students.Compliance;

public class StudentIdentityComplianceDto
{
	public Guid StudentId { get; set; }
	public string? AadhaarMasked { get; set; }
	public string? AadhaarName { get; set; }
	public IdentityVerificationStatus AadhaarVerificationStatus { get; set; }
	public string? ApaarId { get; set; }
	public ConsentStatus ApaarConsentStatus { get; set; }
	public DateTimeOffset? ApaarConsentDate { get; set; }
	public IdentityVerificationStatus ApaarVerificationStatus { get; set; }
	public string? PenNumber { get; set; }
	public string? UdiseStudentCode { get; set; }
	public UdiseSyncStatus UdiseSyncStatus { get; set; }
	public DateTimeOffset? UdiseLastExportedAt { get; set; }
	public bool RteStatus { get; set; }
	public ScholarshipStatus ScholarshipStatus { get; set; }
	public string? ScholarshipSchemeName { get; set; }
	public string? ScholarshipApplicationNo { get; set; }
	public string? CasteCertificateNo { get; set; }
	public IdentityVerificationStatus CasteCertificateVerifiedStatus { get; set; }
}

public class UpdateStudentIdentityComplianceDto
{
	public string? AadhaarNo { get; set; }
	public string? AadhaarName { get; set; }
	public IdentityVerificationStatus AadhaarVerificationStatus { get; set; } = IdentityVerificationStatus.Pending;
	public string? ApaarId { get; set; }
	public ConsentStatus ApaarConsentStatus { get; set; } = ConsentStatus.NotAsked;
	public DateTimeOffset? ApaarConsentDate { get; set; }
	public IdentityVerificationStatus ApaarVerificationStatus { get; set; } = IdentityVerificationStatus.Pending;
	public string? PenNumber { get; set; }
	public string? UdiseStudentCode { get; set; }
	public UdiseSyncStatus UdiseSyncStatus { get; set; } = UdiseSyncStatus.NotReady;
	public bool RteStatus { get; set; }
	public ScholarshipStatus ScholarshipStatus { get; set; } = ScholarshipStatus.NotApplicable;
	public string? ScholarshipSchemeName { get; set; }
	public string? ScholarshipApplicationNo { get; set; }
	public string? CasteCertificateNo { get; set; }
	public IdentityVerificationStatus CasteCertificateVerifiedStatus { get; set; } = IdentityVerificationStatus.NotProvided;
}

public class UpdateAadhaarVerificationStatusDto
{
	public IdentityVerificationStatus Status { get; set; }
}

public class UpdateApaarConsentDto
{
	public ConsentStatus ConsentStatus { get; set; }
	public DateTimeOffset? ConsentDate { get; set; }
}
