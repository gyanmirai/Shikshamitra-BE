using System;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryQualificationEmail_Tests
{
	[Fact]
	public void First_interest_creates_a_durable_intent_and_later_interest_does_not_restart_delivery()
	{
		var inquiry = Create();
		AdmissionInquiryQualificationEmailDispatcher.IsDue(inquiry, DateTime.UtcNow).ShouldBeFalse();
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		var requested = inquiry.GetProperty<DateTime?>("QualificationEmailRequestedAt");
		requested.ShouldNotBeNull();
		AdmissionInquiryQualificationEmailDispatcher.IsDue(inquiry, DateTime.UtcNow).ShouldBeTrue();
		inquiry.SetProperty("QualificationEmailStatus", "Sent");
		inquiry.SetStatus(AdmissionInquiryStatus.NotInterested);
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		inquiry.GetProperty<DateTime?>("QualificationEmailRequestedAt").ShouldBe(requested);
		AdmissionInquiryQualificationEmailDispatcher.IsDue(inquiry, DateTime.UtcNow).ShouldBeFalse();
	}

	[Theory]
	[InlineData("Pending")]
	[InlineData("PendingApproval")]
	[InlineData("Processing")]
	public void Provider_and_approval_waits_are_revisited_only_after_the_saved_retry_time(string status)
	{
		var inquiry = Create();
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		inquiry.SetProperty("QualificationEmailStatus", status);
		var now = DateTime.UtcNow;
		inquiry.SetProperty("QualificationEmailNextAttemptAt", now.AddMinutes(5));
		AdmissionInquiryQualificationEmailDispatcher.IsDue(inquiry, now).ShouldBeFalse();
		AdmissionInquiryQualificationEmailDispatcher.IsDue(inquiry, now.AddMinutes(6)).ShouldBeTrue();
	}

	[Theory]
	[InlineData("Queued")]
	[InlineData("Skipped")]
	[InlineData("Failed")]
	[InlineData("NeedsReview")]
	public void Completed_or_uncertain_transport_is_never_automatically_duplicated(string status)
	{
		var inquiry = Create();
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		inquiry.SetProperty("QualificationEmailStatus", status);
		AdmissionInquiryQualificationEmailDispatcher.IsDue(inquiry, DateTime.UtcNow.AddDays(1)).ShouldBeFalse();
	}

	private static AdmissionInquiry Create() => new(Guid.NewGuid(), Guid.NewGuid(), "INQ-EMAIL",
		Guid.NewGuid(), Guid.NewGuid(), "Email test", "9876543210", "test@example.invalid",
		AdmissionInquirySource.WalkIn, null);
}
