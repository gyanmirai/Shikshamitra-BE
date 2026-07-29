using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmAdmissionYearSetup_Tests
{
	[Fact]
	public void Admission_year_dates_follow_the_school_workflow_order()
	{
		var start = new DateTime(2027, 1, 1);

		var exception = Should.Throw<BusinessException>(() =>
			CreateSetup(
				inquiryOpenDate: start,
				inquiryCloseDate: start.AddDays(-1),
				registrationCloseDate: start.AddDays(10),
				activationTargetDate: start.AddDays(20)));

		exception.Code.ShouldBe("AdmissionCrm:InvalidAdmissionYearDates");
	}

	[Fact]
	public void Published_setup_is_immutable()
	{
		var setup = CreateSetup();
		setup.Publish("{}", Guid.NewGuid(), DateTime.UtcNow);

		var exception = Should.Throw<BusinessException>(() =>
			setup.Update(
				setup.InquiryOpenDate,
				setup.InquiryCloseDate,
				setup.RegistrationCloseDate,
				setup.ActivationTargetDate,
				false,
				true,
				AdmissionCrmPlacementTiming.DivisionBeforeActivation,
				null,
				"[]",
				"[]",
				"{}"));

		exception.Code.ShouldBe("AdmissionCrm:AdmissionYearSetupNotEditable");
	}

	[Fact]
	public void Inquiry_capture_obeys_the_published_window()
	{
		var setup = CreateSetup();
		setup.Publish("{}", Guid.NewGuid(), DateTime.UtcNow);

		setup.EnsureInquiryCaptureAllowed(setup.InquiryOpenDate.AddDays(1));
		Should.Throw<BusinessException>(() =>
			setup.EnsureInquiryCaptureAllowed(setup.InquiryCloseDate.AddDays(1)))
			.Code.ShouldBe("AdmissionCrm:InquiryWindowClosed");
	}

	[Fact]
	public void Closing_a_published_setup_requires_a_reason()
	{
		var setup = CreateSetup();
		setup.Publish("{}", Guid.NewGuid(), DateTime.UtcNow);

		Should.Throw<ArgumentException>(() =>
			setup.Close(" ", Guid.NewGuid(), DateTime.UtcNow));
	}

	private static AdmissionCrmAdmissionYearSetup CreateSetup(
		DateTime? inquiryOpenDate = null,
		DateTime? inquiryCloseDate = null,
		DateTime? registrationCloseDate = null,
		DateTime? activationTargetDate = null)
	{
		var start = new DateTime(2027, 1, 1);
		return new AdmissionCrmAdmissionYearSetup(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			1,
			inquiryOpenDate ?? start,
			inquiryCloseDate ?? start.AddDays(30),
			registrationCloseDate ?? start.AddDays(45),
			activationTargetDate ?? start.AddDays(60),
			true,
			true,
			AdmissionCrmPlacementTiming.StandardBeforeActivation,
			null,
			"[]",
			"[]",
			"{}");
	}
}
