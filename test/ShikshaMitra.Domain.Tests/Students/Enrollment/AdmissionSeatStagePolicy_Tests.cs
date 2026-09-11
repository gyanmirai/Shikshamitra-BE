using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatStagePolicy_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("{}")]
	[InlineData("{\"seat\":{\"version\":1,\"enabled\":false}}")]
	public void Missing_and_disabled_policies_preserve_basic_behavior(string? json) =>
		AdmissionSeatStagePolicy.Read(json).ShouldBeNull();

	[Fact]
	public void Complete_policy_roundtrips()
	{
		var expected = Policy();
		var actual = AdmissionSeatStagePolicy.Read(JsonSerializer.Serialize(new { seat = expected })).ShouldNotBeNull();
		actual.AllowedCompletionOutcomes.ShouldBe(expected.AllowedCompletionOutcomes);
		(actual with { AllowedCompletionOutcomes = expected.AllowedCompletionOutcomes }).ShouldBe(expected);
	}

	[Fact]
	public void Capacity_and_duration_ranges_are_enforced()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { DefaultCapacity = 0 }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { ReservationDurationHours = 1000 }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { ReservationMode = "unknown" }).Validate());
	}

	[Fact]
	public void Disabled_options_cannot_leave_dependent_configuration_enabled()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { AllowRenewal = false }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { AllowWaitlist = false,
			AllowedCompletionOutcomes = ["confirmed", "waitlisted"] }).Validate());
	}

	[Fact]
	public void Confirmed_is_always_a_completion_outcome()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { AllowedCompletionOutcomes = ["released"] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { AllowedCompletionOutcomes = ["confirmed", "confirmed"] }).Validate());
	}

	private static AdmissionSeatStagePolicy Policy() => new()
	{
		Version = 1, Enabled = true, DefaultCapacity = 40, ReservationMode = "manual",
		ReservationDurationHours = 72, AllowDurationOverride = true,
		MinimumDurationHours = 1, MaximumDurationHours = 720,
		AllowRenewal = true, MaximumRenewals = 1, AllowWaitlist = true,
		AllowedCompletionOutcomes = ["confirmed", "waitlisted"]
	};
}
