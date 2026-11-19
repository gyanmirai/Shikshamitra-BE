using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Policies;

public class CollectionPolicyNumericDtoValidation_Tests
{
	[Fact]
	public void Policy_Input_Rejects_Huge_And_Overprecision_Numbers()
	{
		var input = new CreateUpdateCollectionPolicyDto
		{
			GracePeriodDays = 88888888, MaximumChargingDays = 1000000000,
			FlatAmount = decimal.MaxValue, AmountPerDay = 4444444444444m,
			AmountPerWeek = 1000000m, AmountPerMonth = 1.001m, Percentage = 0.001m,
			MaximumLateFeeAmount = 0m
		};
		var errors = new CreateUpdateCollectionPolicyDtoValidator().Validate(input).Errors;
		errors.Count.ShouldBe(8);
	}

	[Fact]
	public void Simulation_Rejects_Unsafe_Inputs_And_Accepts_Boundaries()
	{
		var validator = new CollectionPolicySimulateRequestDtoValidator();
		validator.Validate(new CollectionPolicySimulateRequestDto { DaysLate = int.MaxValue, OutstandingAmount = decimal.MaxValue }).IsValid.ShouldBeFalse();
		validator.Validate(new CollectionPolicySimulateRequestDto { DaysLate = 365, OutstandingAmount = 999999m, RemainingBalance = 0m }).IsValid.ShouldBeTrue();
		validator.Validate(new CollectionPolicySimulateRequestDto { OutstandingAmount = 1.001m }).IsValid.ShouldBeFalse();
	}
}
