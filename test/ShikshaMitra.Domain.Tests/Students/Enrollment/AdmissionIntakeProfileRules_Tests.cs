using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionIntakeProfileRules_Tests
{
	[Fact]
	public void Inquiry_sources_reject_duplicates_and_unlisted_detail_requirements()
	{
		var duplicate = new AdmissionInquiryIntakeRules
		{
			AllowedSources = [AdmissionInquirySource.Website, AdmissionInquirySource.Website]
		};
		Should.Throw<ArgumentException>(() => duplicate.Validate());

		var unlistedDetail = new AdmissionInquiryIntakeRules
		{
			AllowedSources = [AdmissionInquirySource.Website],
			RequireSourceDetailFor = [AdmissionInquirySource.SchoolFair]
		};
		Should.Throw<ArgumentException>(() => unlistedDetail.Validate());
	}

	[Fact]
	public void Assignment_rules_require_pool_and_valid_capacity()
	{
		Should.Throw<ArgumentException>(() => new AdmissionCounsellorAssignmentRules().Validate());
		Should.Throw<ArgumentException>(() => new AdmissionCounsellorAssignmentRules
		{
			CounsellorUserIds = [Guid.NewGuid()], MaxActiveCasesPerCounsellor = 0
		}.Validate());
		new AdmissionCounsellorAssignmentRules
		{
			CounsellorUserIds = [Guid.NewGuid()], MaxActiveCasesPerCounsellor = 100
		}.Validate();
	}

	[Fact]
	public void Offering_intake_marker_tracks_selected_profiles_and_can_be_cleared()
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GRADE-1", true,
			null, null);
		offering.IntakeConfigurationRequired.ShouldBeFalse();
		offering.RequireIntakeConfiguration();
		offering.IntakeConfigurationRequired.ShouldBeFalse();
		offering.ConfigureIntake(Guid.NewGuid(), Guid.NewGuid());
		offering.IntakeConfigurationRequired.ShouldBeTrue();
		offering.InquiryIntakeProfileId.ShouldNotBeNull();
		offering.CounsellorAssignmentProfileId.ShouldNotBeNull();
		offering.ConfigureIntake(null, null);
		offering.InquiryIntakeProfileId.ShouldBeNull();
		offering.CounsellorAssignmentProfileId.ShouldBeNull();
		offering.IntakeConfigurationRequired.ShouldBeFalse();
	}
}
