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
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, Guid.NewGuid(), null, "Asha", "+91 987-654", null, AdmissionInquirySource.Website, null);
		inquiry.NormalizedPhone.ShouldBe("91987654");
		inquiry.Status.ShouldBe(AdmissionInquiryStatus.New);
	}

	[Fact]
	public void MarkContactedSetsTimestamp()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, Guid.NewGuid(), null, "Asha", "987654", null, AdmissionInquirySource.Phone, null);
		inquiry.MarkContacted(DateTime.UtcNow);
		inquiry.Status.ShouldBe(AdmissionInquiryStatus.Contacted);
		inquiry.ContactedAt.ShouldNotBeNull();
	}

	[Fact]
	public void GeneratesServerInquiryNumber()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), null, Guid.NewGuid(), null, "Asha", "987654", null, AdmissionInquirySource.Manual, null);
		inquiry.InquiryNumber.ShouldStartWith("INQ/");
		inquiry.InquiryNumber.Length.ShouldBeLessThanOrEqualTo(64);
	}
}
