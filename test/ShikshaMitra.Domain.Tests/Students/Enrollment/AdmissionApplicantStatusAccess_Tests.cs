using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionApplicantStatusAccess_Tests
{
	[Fact]
	public void Expired_status_token_cannot_issue_an_otp()
	{
		var now = DateTime.UtcNow;
		var access = CreateAccess(now.AddSeconds(-1), now.AddDays(-1));

		var exception = Should.Throw<BusinessException>(() =>
			access.EnsureCanIssueOtp(now, TimeSpan.FromSeconds(30)));

		exception.Code.ShouldBe("AdmissionCrm:StatusTokenExpired");
	}

	[Fact]
	public void Otp_resend_is_rate_limited_and_expired_otp_cannot_be_verified()
	{
		var now = DateTime.UtcNow;
		var access = CreateAccess(now.AddDays(1), now);
		access.IssueOtp("otp-hash", "encrypted-otp", now, now.AddMinutes(5));

		var cooldown = Should.Throw<BusinessException>(() =>
			access.EnsureCanIssueOtp(now.AddSeconds(10), TimeSpan.FromSeconds(30)));
		var expired = Should.Throw<BusinessException>(() =>
			access.EnsureCanVerify(now.AddMinutes(5)));

		cooldown.Code.ShouldBe("AdmissionCrm:StatusOtpResendTooSoon");
		expired.Code.ShouldBe("AdmissionCrm:StatusOtpExpired");
	}

	[Fact]
	public void Otp_is_locked_after_five_failures_even_when_the_next_code_is_correct()
	{
		var now = DateTime.UtcNow;
		var access = CreateAccess(now.AddDays(1), now);
		access.IssueOtp("otp-hash", "encrypted-otp", now, now.AddMinutes(5));
		for (var attempt = 0; attempt < 5; attempt++)
		{
			access.RecordOtpFailure(now.AddSeconds(attempt));
		}

		var exception = Should.Throw<BusinessException>(() => access.MarkOtpVerified(now.AddMinutes(1)));

		exception.Code.ShouldBe("AdmissionCrm:StatusOtpLocked");
	}

	[Fact]
	public void Tampered_or_expired_access_session_is_rejected()
	{
		var now = DateTime.UtcNow;
		var access = CreateVerifiedAccess(now);
		access.IssueAccessSession("session-hash", now.AddMinutes(15), now);

		var tampered = Should.Throw<BusinessException>(() =>
			access.EnsureAccessSession("different-hash", now.AddMinutes(1)));
		var expired = Should.Throw<BusinessException>(() =>
			access.EnsureAccessSession("session-hash", now.AddMinutes(15)));

		tampered.Code.ShouldBe("AdmissionCrm:InvalidApplicantAccessSession");
		expired.Code.ShouldBe("AdmissionCrm:ApplicantAccessSessionExpired");
	}

	[Fact]
	public void Reissuing_the_status_link_invalidates_prior_otp_and_access_session()
	{
		var now = DateTime.UtcNow;
		var access = CreateVerifiedAccess(now);
		access.IssueAccessSession("old-session-hash", now.AddMinutes(15), now);

		access.ReissueLink("new-token-hash", "new-encrypted-token", now.AddDays(2), now.AddMinutes(1));

		access.OtpCodeHash.ShouldBeNull();
		access.OtpVerifiedAt.ShouldBeNull();
		var exception = Should.Throw<BusinessException>(() =>
			access.EnsureAccessSession("old-session-hash", now.AddMinutes(2)));
		exception.Code.ShouldBe("AdmissionCrm:ApplicantAccessSessionRequired");
	}

	[Fact]
	public void Verified_guardian_session_can_be_shared_with_a_related_application_in_the_same_tenant()
	{
		var now = DateTime.UtcNow;
		var tenantId = Guid.NewGuid();
		var guardianPersonId = Guid.NewGuid();
		var verified = CreateAccess(now.AddDays(1), now, tenantId);
		verified.IssueFamilyOtp(
			"otp-hash", "encrypted-otp", now, now.AddMinutes(5), guardianPersonId);
		verified.MarkOtpVerified(now.AddSeconds(1));
		var related = CreateAccess(now.AddDays(1), now, tenantId);

		related.ShareVerifiedFamilyAccessSession(
			verified,
			"family-session-hash",
			now.AddMinutes(15),
			now);

		Should.NotThrow(() =>
			related.EnsureAccessSession("family-session-hash", now.AddMinutes(1)));
		related.FamilyAccessGuardianPersonId.ShouldBe(guardianPersonId);
	}

	[Fact]
	public void Family_session_cannot_cross_tenants()
	{
		var now = DateTime.UtcNow;
		var verified = CreateVerifiedAccess(now, Guid.NewGuid());
		var unrelated = CreateAccess(now.AddDays(1), now, Guid.NewGuid());

		var exception = Should.Throw<BusinessException>(() =>
			unrelated.ShareVerifiedFamilyAccessSession(
				verified,
				"family-session-hash",
				now.AddMinutes(15),
				now));

		exception.Code.ShouldBe("AdmissionCrm:StatusOtpNotVerified");
	}

	private static AdmissionApplicantStatusAccess CreateVerifiedAccess(DateTime now)
		=> CreateVerifiedAccess(now, Guid.NewGuid());

	private static AdmissionApplicantStatusAccess CreateVerifiedAccess(DateTime now, Guid tenantId)
	{
		var access = CreateAccess(now.AddDays(1), now, tenantId);
		access.IssueOtp("otp-hash", "encrypted-otp", now, now.AddMinutes(5));
		access.MarkOtpVerified(now.AddSeconds(1));
		return access;
	}

	private static AdmissionApplicantStatusAccess CreateAccess(DateTime expiresAt, DateTime issuedAt) =>
		CreateAccess(expiresAt, issuedAt, Guid.NewGuid());

	private static AdmissionApplicantStatusAccess CreateAccess(
		DateTime expiresAt,
		DateTime issuedAt,
		Guid tenantId) =>
		new(
			Guid.NewGuid(),
			tenantId,
			Guid.NewGuid(),
			"token-hash",
			"encrypted-token",
			expiresAt,
			issuedAt);
}
