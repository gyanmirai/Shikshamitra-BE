using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferStagePolicy_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("{}")]
	[InlineData("{\"offer\":{\"version\":1,\"enabled\":false}}")]
	public void Missing_And_Disabled_Policies_Preserve_Basic_Behavior(string? json) =>
		AdmissionOfferStagePolicy.Read(json).ShouldBeNull();

	[Fact]
	public void Complete_Policy_Roundtrips()
	{
		var expected = Policy();
		var actual = AdmissionOfferStagePolicy.Read(JsonSerializer.Serialize(new { offer = expected })).ShouldNotBeNull();
		actual.AllowedCompletionOutcomes.ShouldBe(expected.AllowedCompletionOutcomes);
		(actual with { AllowedCompletionOutcomes = expected.AllowedCompletionOutcomes }).ShouldBe(expected);
	}

	[Fact]
	public void Conditional_Offer_Requires_Conditions() =>
		Should.Throw<ArgumentException>(() => (Policy() with { OfferType = "conditional", Conditions = null }).Validate());

	[Fact]
	public void Validity_Range_Must_Contain_Default()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { ValidityDays = 100 }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { MinimumValidityDays = 91 }).Validate());
	}

	[Fact]
	public void Disabled_Exceptions_Must_Have_Zero_Limits()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { AllowExtension = false }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { AllowReissue = false }).Validate());
		Should.NotThrow(() => (Policy() with { AllowExtension = false, MaximumExtensions = 0,
			AllowReissue = false, MaximumReissues = 0 }).Validate());
	}

	[Fact]
	public void Completion_Outcomes_Must_Be_Supported_And_Unique()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { AllowedCompletionOutcomes = [] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { AllowedCompletionOutcomes = ["accepted", "accepted"] }).Validate());
	}

	private static AdmissionOfferStagePolicy Policy() => new()
	{
		Version = 1, Enabled = true, OfferType = "unconditional", Title = "Admission offer",
		Message = "We are pleased to offer admission.", ValidityDays = 14,
		MinimumValidityDays = 1, MaximumValidityDays = 90, AllowExpiryOverride = true,
		RequireTermsAcceptance = true, RequireDeclineReason = true,
		AllowExtension = true, MaximumExtensions = 2, AllowReissue = true, MaximumReissues = 1,
		AllowedCompletionOutcomes = ["accepted", "declined"], AutoAdvanceOnAcceptance = true
	};
}
