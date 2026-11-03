using System;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ProrationEventCalculation_Tests
{
	private readonly ProrationPolicyCalculationService calculator = new();
	[Theory]
	[InlineData(null, 500)]
	[InlineData(1, 500)]
	[InlineData(2, 1500)]
	public void Retired_minimum_fields_in_saved_json_do_not_inflate_daily_charges(int? withdrawalType, decimal expected)
	{
		var json = JsonSerializer.Serialize(new
		{
			FeeHeadId = Guid.NewGuid(), RuleType = 1, RoundingMode = 0,
			MinimumChargeDays = 25, EarlyWithdrawalMinimumChargeDays = 20,
			EarlyWithdrawalRuleType = withdrawalType, EarlyWithdrawalCutoffDay = 15,
			EarlyWithdrawalBeforeCutoffAction = 1
		});
		var rule = JsonSerializer.Deserialize<ProrationPolicyRuleSnapshot>(json)!;
		var result = calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 4, 30),
			new(2026, 4, 16), new(2026, 4, 20));
		Assert.Equal(5, result.ChargedDays);
		Assert.Equal(expected, result.ProratedAmount);
	}
	private static ProrationPolicyRuleSnapshot Rule(ProrationRuleType admission, ProrationRuleType withdrawal) =>
		new(Guid.NewGuid(), admission, ProrationRoundingMode.Currency, 15,
			ProrationAfterCutoffAction.NoCharge, true, true, withdrawal, 15, ProrationAfterCutoffAction.HalfCharge);

	[Theory]
	[InlineData(true, true, 500)]
	[InlineData(true, false, 1500)]
	[InlineData(false, true, 2000)]
	[InlineData(false, false, 3000)]
	public void Daily_event_switches_control_each_boundary(bool admission, bool withdrawal, decimal expected)
	{
		var rule = Rule(ProrationRuleType.Daily, ProrationRuleType.Daily) with
			{ ApplyOnAdmission = admission, ApplyOnEarlyWithdrawal = withdrawal };
		var result = calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 4, 30), new(2026, 4, 16), new(2026, 4, 20));
		Assert.Equal(expected, result.ProratedAmount);
	}

	[Theory]
	[InlineData(15, 500)]
	[InlineData(16, 1000)]
	public void Withdrawal_monthly_cutoff_is_inclusive(int day, decimal expected)
	{
		var result = calculator.Calculate(Rule(ProrationRuleType.FullMonthCharge, ProrationRuleType.MonthlyCutoff),
			3000m, new(2026, 4, 1), new(2026, 6, 30), new(2026, 4, 1), new(2026, 4, day));
		Assert.Equal(expected, result.ProratedAmount);
	}

	[Fact]
	public void Admission_next_month_and_withdrawal_half_month_are_independent()
	{
		var result = AssignmentAmountCalculator.Calculate(calculator,
			Rule(ProrationRuleType.MonthlyCutoff, ProrationRuleType.MonthlyCutoff), 12000m,
			new(2026, 4, 1), new(2027, 3, 31), new(2026, 8, 16), new(2026, 10, 15));
		Assert.Equal(1500m, result.ProratedAmount); // September full, October half.
		Assert.Equal(new[] { 375m, 375m, 375m, 375m }, AssignmentAmountCalculator.Allocate(result.ProratedAmount, [25m, 25m, 25m, 25m]));
	}

	[Fact]
	public void Both_half_rules_in_same_month_are_applied_once()
	{
		var rule = Rule(ProrationRuleType.MonthlyCutoff, ProrationRuleType.MonthlyCutoff) with
			{ CutoffDay = 10, AfterCutoffAction = ProrationAfterCutoffAction.HalfCharge, EarlyWithdrawalCutoffDay = 20 };
		var result = calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 4, 30), new(2026, 4, 11), new(2026, 4, 20));
		Assert.Equal(1500m, result.ProratedAmount);
	}

	[Fact]
	public void Full_month_model_differs_from_no_proration()
	{
		var monthly = calculator.Calculate(Rule(ProrationRuleType.FullMonthCharge, ProrationRuleType.FullMonthCharge),
			3000m, new(2026, 4, 1), new(2026, 6, 30), new(2026, 5, 31), new(2026, 5, 31));
		var full = calculator.Calculate(Rule(ProrationRuleType.FullCharge, ProrationRuleType.FullCharge),
			3000m, new(2026, 4, 1), new(2026, 6, 30), new(2026, 5, 31), new(2026, 5, 31));
		Assert.Equal(1000m, monthly.ProratedAmount);
		Assert.Equal(3000m, full.ProratedAmount);
	}

	[Fact]
	public void Mixed_monthly_and_daily_rules_reduce_only_the_daily_boundary_month()
	{
		var result = calculator.Calculate(Rule(ProrationRuleType.MonthlyCutoff, ProrationRuleType.Daily),
			6000m, new(2026, 4, 1), new(2026, 5, 31), new(2026, 4, 15), new(2026, 5, 1));
		Assert.Equal(3097m, result.ProratedAmount);
	}

	[Fact]
	public void Leap_day_charges_only_the_eligible_day()
	{
		var rule = Rule(ProrationRuleType.Daily, ProrationRuleType.Daily) with
			{ RoundingMode = ProrationRoundingMode.RoundUpRupee };
		var result = calculator.Calculate(rule, 2900m, new(2028, 2, 1), new(2028, 2, 29), new(2028, 2, 29), new(2028, 2, 29));
		Assert.Equal(1, result.ChargedDays);
		Assert.Equal(100m, result.ProratedAmount);
	}

	[Fact]
	public void No_overlap_is_zero_even_with_events_disabled()
	{
		var rule = Rule(ProrationRuleType.Daily, ProrationRuleType.Daily) with { ApplyOnAdmission = false, ApplyOnEarlyWithdrawal = false };
		Assert.Equal(0m, calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 4, 30), new(2026, 5, 1), new(2026, 5, 31)).ProratedAmount);
	}

	[Fact]
	public void Legacy_json_keeps_original_daily_and_monthly_semantics()
	{
		var json = "{\"FeeHeadId\":\"d30b50a3-989b-4be7-b8a9-1cab0ea87704\",\"RuleType\":1,\"RoundingMode\":0,\"MinimumChargeDays\":0}";
		var rule = JsonSerializer.Deserialize<ProrationPolicyRuleSnapshot>(json)!;
		Assert.True(rule.ApplyOnAdmission);
		Assert.True(rule.ApplyOnEarlyWithdrawal);
		Assert.Null(rule.EarlyWithdrawalRuleType);
		Assert.Equal(500m, calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 4, 30), new(2026, 4, 16), new(2026, 4, 20)).ProratedAmount);
	}

	[Fact]
	public void Explicit_withdrawal_daily_charges_one_eligible_day()
	{
		var rule = Rule(ProrationRuleType.Daily, ProrationRuleType.Daily) with
			{ ApplyOnAdmission = false };
		Assert.Equal(100m, calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 4, 30), new(2026, 4, 1), new(2026, 4, 1)).ProratedAmount);
	}

	[Fact]
	public void Legacy_full_charge_stays_full_when_admission_switch_is_disabled()
	{
		var rule = new ProrationPolicyRuleSnapshot(Guid.NewGuid(), ProrationRuleType.FullCharge,
			ProrationRoundingMode.Currency, null, null, ApplyOnAdmission: false);
		Assert.Equal(3000m, calculator.Calculate(rule, 3000m, new(2026, 4, 1), new(2026, 6, 30), new(2026, 4, 1), new(2026, 4, 1)).ProratedAmount);
	}

	[Fact]
	public void Withdrawal_cutoff_31_clamps_to_february_and_keeps_rounding_once()
	{
		var rule = Rule(ProrationRuleType.FullMonthCharge, ProrationRuleType.MonthlyCutoff) with
			{ EarlyWithdrawalCutoffDay = 31, RoundingMode = ProrationRoundingMode.NearestRupee };
		var result = calculator.Calculate(rule, 1999m, new(2028, 2, 1), new(2028, 3, 31), new(2028, 2, 1), new(2028, 2, 29));
		Assert.Equal(499.75m, result.RawAmount);
		Assert.Equal(500m, result.ProratedAmount);
	}
}
