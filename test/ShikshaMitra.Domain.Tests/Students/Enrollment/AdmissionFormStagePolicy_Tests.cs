using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormStagePolicy_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("{}")]
	[InlineData("{\"formDefinitionId\":\"published-form\"}")]
	[InlineData("{\"form\":{\"version\":1,\"enabled\":false}}")]
	public void Missing_And_Disabled_Policies_Preserve_Basic_Form_Behavior(string? json) =>
		AdmissionFormStagePolicy.Read(json).ShouldBeNull();

	[Fact]
	public void Enabled_Policy_Roundtrips_Alongside_The_Attached_Form()
	{
		var expected = Policy() with
		{
			Responsibility = "shared",
			RequireSectionVerification = true,
			VerificationSectionCodes = ["STUDENT", "GUARDIAN"]
		};
		var json = JsonSerializer.Serialize(new { formDefinitionId = Guid.NewGuid(), form = expected });
		var actual = AdmissionFormStagePolicy.Read(json).ShouldNotBeNull();
		(actual with { VerificationSectionCodes = expected.VerificationSectionCodes }).ShouldBe(expected);
		actual.VerificationSectionCodes.ShouldBe(expected.VerificationSectionCodes);
	}

	[Theory]
	[InlineData("school")]
	[InlineData("")]
	public void Unsupported_Responsibility_Is_Rejected(string responsibility) =>
		Should.Throw<ArgumentException>(() => (Policy() with { Responsibility = responsibility }).Validate());

	[Theory]
	[InlineData(0)]
	[InlineData(366)]
	public void Due_Date_Must_Be_Within_One_Year(int days) =>
		Should.Throw<ArgumentException>(() => (Policy() with { DueInDays = days }).Validate());

	[Fact]
	public void Corrections_Require_A_Resubmission_Attempt() =>
		Should.Throw<ArgumentException>(() => (Policy() with { MaxSubmissionAttempts = 1 }).Validate());

	[Fact]
	public void Verification_Requires_At_Least_One_Unique_Valid_Section()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { RequireSectionVerification = true }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with
		{
			RequireSectionVerification = true,
			VerificationSectionCodes = ["STUDENT", "STUDENT"]
		}).Validate());
	}

	[Fact]
	public void Complete_Managed_Policy_Is_Valid() => Should.NotThrow(() => Policy().Validate());

	private static AdmissionFormStagePolicy Policy() => new()
	{
		Version = 1,
		Enabled = true,
		Responsibility = "applicant",
		AllowSaveAndResume = true,
		AllowLateSubmission = true,
		AutoAdvanceOnSubmit = true,
		AllowCorrectionRequests = true,
		MaxSubmissionAttempts = 3
	};
}
