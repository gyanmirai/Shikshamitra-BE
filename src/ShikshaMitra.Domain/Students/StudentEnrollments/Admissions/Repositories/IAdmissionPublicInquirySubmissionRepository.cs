using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionPublicInquirySubmissionRepository
{
	Task<string?> FindFingerprintAsync(Guid? tenantId, Guid cycleId, Guid linkId, Guid? requestId, string fingerprint);
}

public static class AdmissionPublicInquirySubmissionProperties
{
	public const string LinkId = "AdmissionPublicSubmissionLinkId";
	public const string RequestId = "AdmissionPublicSubmissionRequestId";
	public const string Fingerprint = "AdmissionPublicSubmissionFingerprint";
}
