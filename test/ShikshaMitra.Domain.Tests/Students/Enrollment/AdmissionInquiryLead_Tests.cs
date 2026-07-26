using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionInquiryLead_Tests
{
	[Fact]
	public void Quick_inquiry_rejects_a_malformed_email_in_the_domain()
	{
		var exception = Should.Throw<BusinessException>(() =>
			CreateLead("parent.example.com"));

		exception.Code.ShouldBe("AdmissionCrm:InvalidEmail");
	}

	[Fact]
	public void Quick_inquiry_preserves_structured_names_and_normalizes_email_for_matching()
	{
		var lead = CreateLead("  Parent.Contact@Example.COM  ");

		lead.FirstName.ShouldBe("Aarav");
		lead.MiddleName.ShouldBe("Kumar");
		lead.LastName.ShouldBe("Patel");
		lead.Email.ShouldBe("Parent.Contact@Example.COM");
		lead.NormalizedEmail.ShouldBe("parent.contact@example.com");
	}

	[Fact]
	public void Stale_lead_requires_the_dedicated_reopen_workflow()
	{
		var lead = CreateLead("stale.parent@example.com");
		lead.MarkStale();

		var exception = Should.Throw<BusinessException>(() =>
			lead.ChangeStage(AdmissionInquiryStage.FollowUp));

		exception.Code.ShouldBe("AdmissionCrm:StaleLeadRequiresReopen");
		lead.Stage.ShouldBe(AdmissionInquiryStage.Stale);

		var reopenedAt = DateTime.UtcNow;
		lead.Reopen("Parent resumed the admission inquiry.", reopenedAt);

		lead.Stage.ShouldBe(AdmissionInquiryStage.FollowUp);
		lead.ReopenedAt.ShouldBe(reopenedAt);
		lead.ReopenReason.ShouldBe("Parent resumed the admission inquiry.");
	}

	private static AdmissionInquiryLead CreateLead(string email)
	{
		return new AdmissionInquiryLead(
			id: Guid.NewGuid(),
			tenantId: Guid.NewGuid(),
			academicYearId: Guid.NewGuid(),
			inquirySourceId: null,
			inquirySourceSnapshot: null,
			channel: AdmissionInquiryChannel.QuickCallEntry,
			campaignName: null,
			campusBranchName: null,
			preferredCommunicationLanguage: null,
			firstName: "  Aarav ",
			middleName: " Kumar ",
			lastName: " Patel  ",
			dateOfBirth: null,
			parentGuardianName: "Nisha Patel",
			primaryContactNo: "9876543210",
			email: email,
			admissionIntent: "Grade 1 admission",
			targetStandardId: null,
			desiredClassName: "Grade 1",
			notes: null,
			createdAt: DateTimeOffset.UtcNow);
	}
}
