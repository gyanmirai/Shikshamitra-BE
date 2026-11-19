using System;
using System.Linq;
using Volo.Abp.Data;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionInquiry_Tests
{
	[Fact]
	public void NormalizesPhoneAndStartsNew() 
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, "INQ-2026-000001", Guid.NewGuid(), null, "Asha", "+91 987-654", null, AdmissionInquirySource.Website, null);
		inquiry.NormalizedPhone.ShouldBe("91987654");
		inquiry.Status.ShouldBe(AdmissionInquiryStatus.New);
	}

	[Fact]
	public void MarkContactedSetsTimestamp()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, "INQ-2026-000001", Guid.NewGuid(), null, "Asha", "987654", null, AdmissionInquirySource.Phone, null);
		inquiry.MarkContacted(DateTime.UtcNow);
		inquiry.Status.ShouldBe(AdmissionInquiryStatus.New);
		inquiry.ContactedAt.ShouldNotBeNull();
	}

	[Fact]
	public void Contact_does_not_clear_confirmed_interest_and_email_intent_is_recorded_once()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, "INQ-2026-000001", Guid.NewGuid(), Guid.NewGuid(), "Asha", "9876543210", null, AdmissionInquirySource.Phone, null);
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		var requestedAt = inquiry.GetProperty<DateTime?>("QualificationEmailRequestedAt");
		requestedAt.ShouldNotBeNull();
		inquiry.MarkContacted(DateTime.UtcNow);
		inquiry.Status.ShouldBe(AdmissionInquiryStatus.Interested);
		inquiry.SetStatus(AdmissionInquiryStatus.NotInterested);
		inquiry.SetStatus(AdmissionInquiryStatus.Interested);
		inquiry.GetProperty<DateTime?>("QualificationEmailRequestedAt").ShouldBe(requestedAt);
		inquiry.GetDistributedEvents().Count().ShouldBe(1);
	}

	[Theory]
	[InlineData(AdmissionInquiryStatus.Contacted, AdmissionInquiryStatus.New)]
	[InlineData(AdmissionInquiryStatus.FollowUp, AdmissionInquiryStatus.New)]
	[InlineData(AdmissionInquiryStatus.ApplicationStarted, AdmissionInquiryStatus.Interested)]
	[InlineData(AdmissionInquiryStatus.Converted, AdmissionInquiryStatus.Interested)]
	[InlineData(AdmissionInquiryStatus.Lost, AdmissionInquiryStatus.NotInterested)]
	[InlineData(AdmissionInquiryStatus.Duplicate, AdmissionInquiryStatus.NotInterested)]
	public void Historical_statuses_have_three_business_labels_without_changing_lifecycle(AdmissionInquiryStatus raw, AdmissionInquiryStatus display)
	{
		AdmissionInquiryStatusRules.Display(raw).ShouldBe(display);
		AdmissionInquiryStatusRules.IsTransferred(raw).ShouldBe(raw is AdmissionInquiryStatus.ApplicationStarted or AdmissionInquiryStatus.Converted);
	}

	[Fact]
	public void PreservesAllocatedInquiryNumber()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, "INQ-2026-000042", Guid.NewGuid(), null, "Asha", "987654", null, AdmissionInquirySource.Manual, null);
		inquiry.InquiryNumber.ShouldBe("INQ-2026-000042");
		inquiry.InquiryNumber.Length.ShouldBeLessThanOrEqualTo(64);
	}

	[Fact]
	public void UpdatesEditableDetailsAndPreferredLanguage()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, "INQ-2026-000001", Guid.NewGuid(), null, "Asha",
			"987654", null, AdmissionInquirySource.Manual, null);

		inquiry.UpdateDetails(" Asha Rao ", "+91 999-888", " FAMILY@EXAMPLE.COM ",
			AdmissionInquirySource.Referral, " Parent referral ", "HI");

		inquiry.ApplicantName.ShouldBe("Asha Rao");
		inquiry.NormalizedPhone.ShouldBe("91999888");
		inquiry.Email.ShouldBe("family@example.com");
		inquiry.SourceDetail.ShouldBe("Parent referral");
		inquiry.PreferredLanguageCode.ShouldBe("hi");
	}
}
