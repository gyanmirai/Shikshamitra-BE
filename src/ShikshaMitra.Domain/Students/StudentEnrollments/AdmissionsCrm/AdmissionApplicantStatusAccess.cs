using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionApplicantStatusAccess : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const int MaximumFailedAttempts = 5;

	public Guid? TenantId { get; private set; }
	public Guid LeadId { get; private set; }
	public string TokenHash { get; private set; } = string.Empty;
	public string? EncryptedToken { get; private set; }
	public DateTime TokenExpiresAt { get; private set; }
	public DateTime LinkIssuedAt { get; private set; }
	public string? OtpCodeHash { get; private set; }
	public string? EncryptedOtpCode { get; private set; }
	public DateTime? OtpIssuedAt { get; private set; }
	public DateTime? OtpExpiresAt { get; private set; }
	public DateTime? OtpVerifiedAt { get; private set; }
	public string? AccessSessionTokenHash { get; private set; }
	public DateTime? AccessSessionExpiresAt { get; private set; }
	public Guid? FamilyAccessGuardianPersonId { get; private set; }
	public int OtpFailedAttemptCount { get; private set; }
	public DateTime? LastOtpFailureAt { get; private set; }

	protected AdmissionApplicantStatusAccess()
	{
	}

	public AdmissionApplicantStatusAccess(
		Guid id,
		Guid? tenantId,
		Guid leadId,
		string tokenHash,
		string? encryptedToken,
		DateTime tokenExpiresAt,
		DateTime linkIssuedAt)
		: base(id)
	{
		TenantId = tenantId;
		LeadId = leadId;
		TokenHash = Check.NotNullOrWhiteSpace(
			tokenHash,
			nameof(tokenHash),
			maxLength: AdmissionCrmConsts.MaxTokenHashLength);
		EncryptedToken = NormalizeOptional(
			encryptedToken,
			nameof(encryptedToken),
			AdmissionCrmConsts.MaxEncryptedTokenLength);
		TokenExpiresAt = tokenExpiresAt;
		LinkIssuedAt = linkIssuedAt;
	}

	public void IssueOtp(
		string otpCodeHash,
		string encryptedOtpCode,
		DateTime issuedAt,
		DateTime expiresAt)
	{
		EnsureTokenActive(issuedAt);
		OtpCodeHash = Check.NotNullOrWhiteSpace(
			otpCodeHash,
			nameof(otpCodeHash),
			maxLength: AdmissionCrmConsts.MaxOtpHashLength);
		EncryptedOtpCode = Check.NotNullOrWhiteSpace(
			encryptedOtpCode,
			nameof(encryptedOtpCode),
			maxLength: AdmissionCrmConsts.MaxEncryptedOtpLength);
		OtpIssuedAt = issuedAt;
		OtpExpiresAt = expiresAt;
		OtpVerifiedAt = null;
		AccessSessionTokenHash = null;
		AccessSessionExpiresAt = null;
		OtpFailedAttemptCount = 0;
		LastOtpFailureAt = null;
		FamilyAccessGuardianPersonId = null;
	}

	public void IssueFamilyOtp(
		string otpCodeHash,
		string encryptedOtpCode,
		DateTime issuedAt,
		DateTime expiresAt,
		Guid? guardianPersonId)
	{
		IssueOtp(otpCodeHash, encryptedOtpCode, issuedAt, expiresAt);
		FamilyAccessGuardianPersonId = guardianPersonId;
	}

	public void ReissueLink(
		string tokenHash,
		string encryptedToken,
		DateTime expiresAt,
		DateTime issuedAt)
	{
		TokenHash = Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash), AdmissionCrmConsts.MaxTokenHashLength);
		EncryptedToken = Check.NotNullOrWhiteSpace(encryptedToken, nameof(encryptedToken), AdmissionCrmConsts.MaxEncryptedTokenLength);
		TokenExpiresAt = expiresAt;
		LinkIssuedAt = issuedAt;
		OtpCodeHash = null;
		EncryptedOtpCode = null;
		OtpIssuedAt = null;
		OtpExpiresAt = null;
		OtpVerifiedAt = null;
		OtpFailedAttemptCount = 0;
		LastOtpFailureAt = null;
		AccessSessionTokenHash = null;
		AccessSessionExpiresAt = null;
		FamilyAccessGuardianPersonId = null;
	}

	public void EnsureCanIssueOtp(DateTime now, TimeSpan resendCooldown)
	{
		EnsureTokenActive(now);

		if (OtpIssuedAt.HasValue && OtpIssuedAt.Value.Add(resendCooldown) > now)
		{
			var retryAfterSeconds = (int)Math.Ceiling(
				(OtpIssuedAt.Value.Add(resendCooldown) - now).TotalSeconds);
			throw new BusinessException("AdmissionCrm:StatusOtpResendTooSoon")
				.WithData("RetryAfterSeconds", retryAfterSeconds);
		}
	}

	public void EnsureCanVerify(DateTime now)
	{
		EnsureTokenActive(now);

		if (string.IsNullOrWhiteSpace(OtpCodeHash) || !OtpExpiresAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:StatusOtpNotIssued");
		}

		if (OtpExpiresAt.Value <= now)
		{
			throw new BusinessException("AdmissionCrm:StatusOtpExpired");
		}

		if (OtpFailedAttemptCount >= MaximumFailedAttempts)
		{
			throw new BusinessException("AdmissionCrm:StatusOtpLocked");
		}
	}

	public void MarkOtpVerified(DateTime verifiedAt)
	{
		EnsureCanVerify(verifiedAt);
		OtpVerifiedAt = verifiedAt;
	}

	public void IssueAccessSession(string sessionTokenHash, DateTime expiresAt, DateTime issuedAt)
	{
		EnsureTokenActive(issuedAt);
		if (!OtpVerifiedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:StatusOtpNotVerified");
		}
		AccessSessionTokenHash = Check.NotNullOrWhiteSpace(
			sessionTokenHash,
			nameof(sessionTokenHash),
			maxLength: AdmissionCrmConsts.MaxTokenHashLength);
		AccessSessionExpiresAt = expiresAt;
	}

	public void ShareVerifiedFamilyAccessSession(
		AdmissionApplicantStatusAccess verifiedAccess,
		string sessionTokenHash,
		DateTime expiresAt,
		DateTime issuedAt)
	{
		Check.NotNull(verifiedAccess, nameof(verifiedAccess));
		EnsureTokenActive(issuedAt);
		if (verifiedAccess.TenantId != TenantId || !verifiedAccess.OtpVerifiedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:StatusOtpNotVerified");
		}

		AccessSessionTokenHash = Check.NotNullOrWhiteSpace(
			sessionTokenHash,
			nameof(sessionTokenHash),
			maxLength: AdmissionCrmConsts.MaxTokenHashLength);
		AccessSessionExpiresAt = expiresAt;
		FamilyAccessGuardianPersonId = verifiedAccess.FamilyAccessGuardianPersonId;
	}

	public void EnsureAccessSession(string sessionTokenHash, DateTime now)
	{
		EnsureTokenActive(now);
		if (string.IsNullOrWhiteSpace(AccessSessionTokenHash) || !AccessSessionExpiresAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:ApplicantAccessSessionRequired");
		}
		if (AccessSessionExpiresAt.Value <= now)
		{
			throw new BusinessException("AdmissionCrm:ApplicantAccessSessionExpired");
		}
		if (!string.Equals(AccessSessionTokenHash, sessionTokenHash, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:InvalidApplicantAccessSession");
		}
	}

	public void RecordOtpFailure(DateTime failedAt)
	{
		OtpFailedAttemptCount++;
		LastOtpFailureAt = failedAt;
	}

	private void EnsureTokenActive(DateTime now)
	{
		if (TokenExpiresAt <= now)
		{
			throw new BusinessException("AdmissionCrm:StatusTokenExpired");
		}
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
