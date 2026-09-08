using System;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ProrationPolicyCalculationService_Tests
{
	private readonly ProrationPolicyCalculationService service = new();

	[Fact]
	public void Daily_rule_uses_inclusive_days_and_currency_rounding()
	{
		var result = service.Calculate(
			Rule(ProrationRuleType.Daily),
			10000m,
			new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
			new DateTime(2026, 4, 16), new DateTime(2026, 4, 30));

		Assert.Equal(30, result.CoverageDays);
		Assert.Equal(15, result.ActualEligibleDays);
		Assert.Equal(0.5m, result.Factor);
		Assert.Equal(5000m, result.ProratedAmount);
	}

	[Fact]
	public void One_day_period_never_becomes_zero()
	{
		var result = service.Calculate(
			Rule(ProrationRuleType.Daily),
			3100m,
			new DateTime(2026, 5, 1), new DateTime(2026, 5, 31),
			new DateTime(2026, 5, 31), new DateTime(2026, 5, 31));

		Assert.Equal(1, result.ActualEligibleDays);
		Assert.Equal(100m, result.ProratedAmount);
	}

	[Fact]
	public void Daily_rule_handles_leap_day()
	{
		var result = service.Calculate(
			Rule(ProrationRuleType.Daily),
			2900m,
			new DateTime(2028, 2, 1), new DateTime(2028, 2, 29),
			new DateTime(2028, 2, 29), new DateTime(2028, 2, 29));

		Assert.Equal(29, result.CoverageDays);
		Assert.Equal(100m, result.ProratedAmount);
	}

	[Fact]
	public void Minimum_charge_days_are_capped_at_coverage_days()
	{
		var result = service.Calculate(
			Rule(ProrationRuleType.Daily, minimumChargeDays: 45),
			3000m,
			new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
			new DateTime(2026, 4, 30), new DateTime(2026, 4, 30));

		Assert.Equal(30, result.ChargedDays);
		Assert.Equal(3000m, result.ProratedAmount);
	}

	[Theory]
	[InlineData(15, 1)]
	[InlineData(16, 0)]
	public void Monthly_cutoff_boundary_is_deterministic(int admissionDay, int expectedFactor)
	{
		var result = service.Calculate(
			Rule(
				ProrationRuleType.MonthlyCutoff,
				cutoffDay: 15,
				afterCutoffAction: ProrationAfterCutoffAction.NoCharge),
			1000m,
			new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
			new DateTime(2026, 4, admissionDay), new DateTime(2026, 4, 30));

		Assert.Equal((decimal)expectedFactor, result.Factor);
		Assert.Equal(1000m * expectedFactor, result.ProratedAmount);
	}

	[Fact]
	public void Monthly_cutoff_can_charge_half_after_cutoff()
	{
		var result = service.Calculate(
			Rule(
				ProrationRuleType.MonthlyCutoff,
				ProrationRoundingMode.NearestRupee,
				cutoffDay: 15,
				afterCutoffAction: ProrationAfterCutoffAction.HalfCharge),
			999m,
			new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
			new DateTime(2026, 4, 16), new DateTime(2026, 4, 30));

		Assert.Equal(0.5m, result.Factor);
		Assert.Equal(500m, result.ProratedAmount);
	}

	[Fact]
	public void No_overlap_produces_zero_and_no_eligible_dates()
	{
		var result = service.Calculate(
			Rule(ProrationRuleType.Daily),
			1000m,
			new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
			new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

		Assert.Equal(0m, result.ProratedAmount);
		Assert.Null(result.EligibleStartDate);
		Assert.Null(result.EligibleEndDate);
	}

	private static ProrationPolicyRuleSnapshot Rule(
		ProrationRuleType type,
		ProrationRoundingMode rounding = ProrationRoundingMode.Currency,
		int? minimumChargeDays = null,
		int? cutoffDay = null,
		ProrationAfterCutoffAction? afterCutoffAction = null) =>
		new(Guid.NewGuid(), type, rounding, minimumChargeDays, cutoffDay, afterCutoffAction);
}
