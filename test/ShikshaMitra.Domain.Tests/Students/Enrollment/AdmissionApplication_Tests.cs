using System;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionApplication_Tests
{
	[Fact]
	public void StartsAsDraftAndCanSubmitOnce()
	{
		var application = new AdmissionApplication(Guid.NewGuid(), null, "APP/2026/ABC", Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		application.Status.ShouldBe(AdmissionApplicationStatus.Draft);
		application.Submit();
		application.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
		Should.Throw<Volo.Abp.BusinessException>(() => application.Submit());
	}

	[Fact]
	public void SupportsReviewOfferAcceptanceAndEnrollment()
	{
		var application = CreateSubmittedApplication();

		application.MarkInReview(Guid.NewGuid(), "Academic review started");
		application.Approve(Guid.NewGuid(), "Meets school criteria");
		application.IssueOffer(DateTime.UtcNow.AddDays(14), Guid.NewGuid(), "Seat reserved for Class 6");
		application.AcceptOffer(Guid.NewGuid(), "Parent confirmed");
		application.MarkEnrolled(Guid.NewGuid(), Guid.NewGuid());

		application.Status.ShouldBe(AdmissionApplicationStatus.Enrolled);
		application.OfferAcceptedAt.ShouldNotBeNull();
		application.EnrolledStudentId.ShouldNotBeNull();
		application.StudentEnrollmentId.ShouldNotBeNull();
	}

	[Fact]
	public void Finalization_links_identity_and_onboarding_without_legacy_enrollment_fields()
	{
		var application = CreateSubmittedApplication();
		application.MarkInReview(Guid.NewGuid(), null);
		application.Approve(Guid.NewGuid(), null);
		application.IssueOffer(DateTime.UtcNow.AddDays(7), Guid.NewGuid(), null);
		application.AcceptOffer(Guid.NewGuid(), null);
		var studentId = Guid.NewGuid();
		var onboardingCaseId = Guid.NewGuid();

		application.FinalizeAdmission(studentId, onboardingCaseId, DateTime.UtcNow);

		application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
		application.StudentId.ShouldBe(studentId);
		application.OnboardingCaseId.ShouldBe(onboardingCaseId);
		application.FinalizedAt.ShouldNotBeNull();
		application.EnrolledStudentId.ShouldBeNull();
		application.StudentEnrollmentId.ShouldBeNull();
	}

	[Fact]
	public void Historical_onboarding_link_preserves_legacy_enrolled_status_and_enrollment()
	{
		var application = CreateSubmittedApplication();
		application.MarkInReview(Guid.NewGuid(), null);
		application.Approve(Guid.NewGuid(), null);
		application.IssueOffer(DateTime.UtcNow.AddDays(7), Guid.NewGuid(), null);
		application.AcceptOffer(Guid.NewGuid(), null);
		var studentId = Guid.NewGuid();
		var enrollmentId = Guid.NewGuid();
		var caseId = Guid.NewGuid();
		application.MarkEnrolled(studentId, enrollmentId);

		application.LinkHistoricalOnboarding(studentId, caseId);
		application.LinkHistoricalOnboarding(studentId, caseId);

		application.Status.ShouldBe(AdmissionApplicationStatus.Enrolled);
		application.EnrolledStudentId.ShouldBe(studentId);
		application.StudentEnrollmentId.ShouldBe(enrollmentId);
		application.StudentId.ShouldBe(studentId);
		application.OnboardingCaseId.ShouldBe(caseId);
		application.FinalizedAt.ShouldBeNull();
	}

	[Fact]
	public void RequiresReasonForNegativeDecisions()
	{
		var application = CreateSubmittedApplication();

		Should.Throw<Volo.Abp.BusinessException>(() => application.Reject(Guid.NewGuid(), string.Empty));
		Should.Throw<Volo.Abp.BusinessException>(() => application.Waitlist(Guid.NewGuid(), " "));
	}

	[Fact]
	public void RejectsExpiredOfferAcceptanceAndInvalidEnrollmentState()
	{
		var application = CreateSubmittedApplication();
		application.MarkInReview(Guid.NewGuid(), null);
		application.Approve(Guid.NewGuid(), null);
		Should.Throw<Volo.Abp.BusinessException>(() =>
			application.IssueOffer(DateTime.UtcNow.AddMinutes(-1), Guid.NewGuid(), null));
		application.Status.ShouldBe(AdmissionApplicationStatus.Approved);
		Should.Throw<Volo.Abp.BusinessException>(() => application.MarkEnrolled(Guid.NewGuid(), Guid.NewGuid()));
	}

	[Fact]
	public void Allows_waitlist_recovery_and_records_offer_decline()
	{
		var application = CreateSubmittedApplication();

		application.MarkInReview(Guid.NewGuid(), null);
		application.Waitlist(Guid.NewGuid(), "Awaiting seat availability");
		application.Approve(Guid.NewGuid(), "Seat became available");
		application.IssueOffer(DateTime.UtcNow.AddDays(7), Guid.NewGuid(), null);
		application.DeclineOffer(Guid.NewGuid(), "Family selected another school");

		application.Status.ShouldBe(AdmissionApplicationStatus.OfferDeclined);
		application.DecisionReason.ShouldBe("Family selected another school");
		application.OfferExpiresAt.ShouldNotBeNull();
	}

	[Fact]
	public void Supports_offer_extension_withdrawal_and_reissue()
	{
		var application = CreateSubmittedApplication();
		application.MarkInReview(Guid.NewGuid(), null);
		application.Approve(Guid.NewGuid(), null);
		var firstExpiry = DateTime.UtcNow.AddDays(7);
		application.IssueOffer(firstExpiry, Guid.NewGuid(), null);
		application.ExtendOffer(firstExpiry.AddDays(3));
		application.OfferExpiresAt!.Value.ShouldBeGreaterThan(firstExpiry);
		application.WithdrawOffer(Guid.NewGuid(), "Offer details need correction.");
		application.Status.ShouldBe(AdmissionApplicationStatus.Approved);
		application.ReissueOffer(DateTime.UtcNow.AddDays(14), Guid.NewGuid(), "Corrected offer.");
		application.Status.ShouldBe(AdmissionApplicationStatus.Offered);
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Draft)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	[InlineData(AdmissionApplicationStatus.OfferAccepted)]
	[InlineData(AdmissionApplicationStatus.Enrolled)]
	[InlineData(AdmissionApplicationStatus.AdmissionFinalized)]
	public void Does_not_allow_approval_from_incompatible_state(AdmissionApplicationStatus status)
	{
		var application = CreateSubmittedApplication();
		if (status != AdmissionApplicationStatus.Submitted)
		{
			// Move the aggregate to the requested state through its public lifecycle where possible.
			if (status == AdmissionApplicationStatus.Draft) application.Reopen();
			if (status == AdmissionApplicationStatus.Rejected) application.Reject(Guid.NewGuid(), "Incomplete application");
			if (status == AdmissionApplicationStatus.OfferAccepted)
			{
				application.MarkInReview(Guid.NewGuid(), null);
				application.Approve(Guid.NewGuid(), null);
				application.IssueOffer(DateTime.UtcNow.AddDays(7), Guid.NewGuid(), null);
				application.AcceptOffer(Guid.NewGuid(), null);
			}
			if (status == AdmissionApplicationStatus.Enrolled)
			{
				application.MarkInReview(Guid.NewGuid(), null);
				application.Approve(Guid.NewGuid(), null);
				application.IssueOffer(DateTime.UtcNow.AddDays(7), Guid.NewGuid(), null);
				application.AcceptOffer(Guid.NewGuid(), null);
				application.MarkEnrolled(Guid.NewGuid(), Guid.NewGuid());
			}
			if (status == AdmissionApplicationStatus.AdmissionFinalized)
			{
				application.MarkInReview(Guid.NewGuid(), null);
				application.Approve(Guid.NewGuid(), null);
				application.IssueOffer(DateTime.UtcNow.AddDays(7), Guid.NewGuid(), null);
				application.AcceptOffer(Guid.NewGuid(), null);
				application.FinalizeAdmission(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
			}
		}

		application.Status.ShouldBe(status);
		Should.Throw<Volo.Abp.BusinessException>(() => application.Approve(Guid.NewGuid(), null));
	}

	private static AdmissionApplication CreateSubmittedApplication()
	{
		var application = new AdmissionApplication(Guid.NewGuid(), null, "APP/2026/TEST", Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		application.Submit();
		return application;
	}
}
