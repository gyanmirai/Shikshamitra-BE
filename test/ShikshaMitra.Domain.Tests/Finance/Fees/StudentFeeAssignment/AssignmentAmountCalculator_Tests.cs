using System;
using System.Linq;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class AssignmentAmountCalculator_Tests
{
	[Theory]
	[InlineData(10000, 6000, 4000)]
	[InlineData(8000, 4800, 3200)]
	public void Unequal_percentages_survive_amount_overrides(decimal total, decimal first, decimal last)
	{
		Assert.Equal(new[] { first, last }, AssignmentAmountCalculator.Allocate(total, [60m, 40m]));
	}

	[Fact]
	public void Tiny_amount_never_produces_a_negative_residual()
	{
		var amounts = AssignmentAmountCalculator.Allocate(.02m, [25m, 25m, 25m, 25m]);
		Assert.All(amounts, x => Assert.True(x >= 0));
		Assert.Equal(1m, amounts.Sum());
		Assert.Equal(new[] { 0m, 0m, 0m, 1m }, amounts);
	}

	[Fact]
	public void Invalid_percentage_totals_are_rejected()
	{
		Assert.Throws<BusinessException>(() => AssignmentAmountCalculator.Allocate(100m, [60m, 60m]));
		Assert.Throws<BusinessException>(() => AssignmentAmountCalculator.Allocate(-1m, [100m]));
	}

	[Fact]
	public void Daily_service_charge_is_independent_of_payment_frequency()
	{
		var rule = new ProrationPolicyRuleSnapshot(Guid.NewGuid(), ProrationRuleType.Daily, ProrationRoundingMode.Currency, 0, null, null);
		var result = AssignmentAmountCalculator.Calculate(new(), rule, 12000m, new(2026, 4, 1), new(2027, 3, 31), new(2026, 10, 1), new(2027, 3, 31));
		Assert.Equal(5984m, result.ProratedAmount);
		Assert.Equal(result.ProratedAmount, AssignmentAmountCalculator.Allocate(result.ProratedAmount, [100m]).Sum());
		Assert.Equal(result.ProratedAmount, AssignmentAmountCalculator.Allocate(result.ProratedAmount, [25m, 25m, 25m, 25m]).Sum());
	}

	[Fact]
	public void Monthly_cutoff_prices_service_months_before_quarterly_allocation()
	{
		var rule = new ProrationPolicyRuleSnapshot(Guid.NewGuid(), ProrationRuleType.MonthlyCutoff, ProrationRoundingMode.Currency, null, 15, ProrationAfterCutoffAction.HalfCharge);
		var result = AssignmentAmountCalculator.Calculate(new(), rule, 12000m, new(2026, 4, 1), new(2027, 3, 31), new(2026, 8, 20), new(2027, 3, 31));
		Assert.Equal(7500m, result.ProratedAmount);
		Assert.Equal(new[] { 1875m, 1875m, 1875m, 1875m }, AssignmentAmountCalculator.Allocate(result.ProratedAmount, [25m, 25m, 25m, 25m]));
	}

	[Fact]
	public void Assignment_snapshot_and_due_ownership_are_write_once_and_concession_is_optional()
	{
		var assignment = new StudentFeeStructure(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		Assert.Null(assignment.ConcessionPolicyId);
		assignment.CaptureAssignment(new string('A', 64), "{}");
		Assert.Throws<InvalidOperationException>(() => assignment.CaptureAssignment(new string('B', 64), "{}"));
	}
}
