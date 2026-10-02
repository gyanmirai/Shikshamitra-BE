using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPaymentStagePolicy_Tests
{
	[Fact]
	public void Fee_selection_requirement_is_explicit_and_historical_policies_remain_optional()
	{
		var json = JsonSerializer.Serialize(new { payment = Policy() });
		AdmissionPaymentStagePolicy.Read(json)!.RequiresFeeStructureSelection.ShouldBeFalse();
		var optedIn = Policy() with { RequiresFeeStructureSelection = true };
		AdmissionPaymentStagePolicy.Read(JsonSerializer.Serialize(new { payment = optedIn }))!.RequiresFeeStructureSelection.ShouldBeTrue();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("{}")]
	[InlineData("{\"payment\":{\"version\":1,\"enabled\":false}}")]
	public void Missing_And_Disabled_Policies_Preserve_Basic_Behavior(string? json) =>
		AdmissionPaymentStagePolicy.Read(json).ShouldBeNull();

	[Fact]
	public void Complete_Staff_Policy_Roundtrips()
	{
		var expected = Policy();
		var actual = AdmissionPaymentStagePolicy.Read(
			JsonSerializer.Serialize(new { payment = expected })).ShouldNotBeNull();
		actual.AcceptedMethods.ShouldBe(expected.AcceptedMethods);
		(actual with { AcceptedMethods = expected.AcceptedMethods }).ShouldBe(expected);
	}

	[Theory]
	[InlineData("online", "cash")]
	[InlineData("staff", "onlineGateway")]
	public void Collection_Mode_Rejects_Incompatible_Methods(string mode, string method) =>
		Should.Throw<ArgumentException>(() =>
			(Policy() with { CollectionMode = mode, AcceptedMethods = [method] }).Validate());

	[Fact]
	public void Online_Mode_Requires_Only_The_Gateway() => Should.NotThrow(() =>
		(Policy() with { CollectionMode = "online", AcceptedMethods = ["onlineGateway"] }).Validate());

	[Theory]
	[InlineData(0)]
	[InlineData(10000000.001)]
	public void Amount_Must_Be_Positive_Bounded_And_Currency_Precise(double amount) =>
		Should.Throw<ArgumentException>(() => (Policy() with { Amount = (decimal)amount }).Validate());

	[Fact]
	public void Installments_Require_A_Valid_Minimum()
	{
		Should.Throw<ArgumentException>(() =>
			(Policy() with { AllowInstallments = true, MinimumInstallmentAmount = null }).Validate());
		Should.Throw<ArgumentException>(() =>
			(Policy() with { AllowInstallments = true, MinimumInstallmentAmount = 501m }).Validate());
		Should.NotThrow(() =>
			(Policy() with { AllowInstallments = true, MinimumInstallmentAmount = 100m }).Validate());
	}

	private static AdmissionPaymentStagePolicy Policy() => new()
	{
		Version = 1,
		Enabled = true,
		PurposeCode = "APPLICATION_FEE",
		PurposeName = "Application fee",
		Currency = "INR",
		Amount = 500m,
		CollectionMode = "staff",
		Payer = "either",
		AcceptedMethods = ["cash", "upi"],
		AllowLatePayment = true,
		AutoAdvanceOnSettlement = true
	};
}
