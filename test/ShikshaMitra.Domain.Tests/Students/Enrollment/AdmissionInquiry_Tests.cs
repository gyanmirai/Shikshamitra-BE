using System;
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
		inquiry.Status.ShouldBe(AdmissionInquiryStatus.Contacted);
		inquiry.ContactedAt.ShouldNotBeNull();
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
