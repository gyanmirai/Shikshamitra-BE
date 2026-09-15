using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

/// <summary>Service entitlement is independent of installment frequency.</summary>
public static class AssignmentAmountCalculator
{
	public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> percentages)
	{
		if (total < 0 || decimal.Round(total, 2) != total || percentages.Count == 0 ||
			percentages.Any(x => x < 0 || x > 100) || percentages.Sum() != 100m)
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var amounts = percentages.Select(p => decimal.Round(total * p / 100m, 2, MidpointRounding.AwayFromZero)).ToArray();
		amounts[^1] = total - amounts.Take(amounts.Length - 1).Sum();
		// Tiny totals can make the final residual negative. Remove excess minor units
		// in stable reverse sequence, never generate a negative obligation.
		for (var i = amounts.Length - 2; amounts[^1] < 0 && i >= 0; i--)
		{
			var correction = Math.Min(amounts[i], -amounts[^1]);
			amounts[i] -= correction;
			amounts[^1] += correction;
		}
		return amounts;
	}

	public static ProrationCalculationResult Calculate(
		ProrationPolicyCalculationService calculator, ProrationPolicyRuleSnapshot rule,
		decimal amount, DateTime coverageStart, DateTime coverageEnd, DateTime start, DateTime end)
	{
		if (rule.RuleType != ProrationRuleType.MonthlyCutoff)
			return calculator.Calculate(rule, amount, coverageStart, coverageEnd, start, end);
		var periods = new List<(DateTime Start, DateTime End)>();
		for (var month = new DateTime(coverageStart.Year, coverageStart.Month, 1); month <= coverageEnd; month = month.AddMonths(1))
			periods.Add((month < coverageStart ? coverageStart : month,
				month.AddMonths(1).AddDays(-1) > coverageEnd ? coverageEnd : month.AddMonths(1).AddDays(-1)));
		// Monthly-cutoff annual totals have equal charge-period weights, regardless
		// of whether the payment term is annual, quarterly or monthly.
		var raw = 0m;
		foreach (var period in periods)
		{
			var eligibleStart = start > period.Start ? start : period.Start;
			var eligibleEnd = end < period.End ? end : period.End;
			if (eligibleStart > eligibleEnd) continue;
			if (rule.CutoffDay is null or < 1 or > 31)
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			var cutoff = Math.Min(rule.CutoffDay.Value, DateTime.DaysInMonth(period.Start.Year, period.Start.Month));
			var factor = eligibleStart.Day <= cutoff ? 1m : rule.AfterCutoffAction == ProrationAfterCutoffAction.HalfCharge ? .5m : 0m;
			raw += amount / periods.Count * factor;
		}
		var result = calculator.Calculate(rule with { RuleType = ProrationRuleType.Daily, MinimumChargeDays = 0 },
			amount, coverageStart, coverageEnd, start, end);
		var rounded = rule.RoundingMode switch
		{
			ProrationRoundingMode.NearestRupee => decimal.Round(raw, 0, MidpointRounding.AwayFromZero),
			ProrationRoundingMode.RoundUpRupee => decimal.Ceiling(raw),
			ProrationRoundingMode.RoundDownRupee => decimal.Floor(raw),
			_ => decimal.Round(raw, 2, MidpointRounding.AwayFromZero)
		};
		return result with { ProratedAmount = Math.Min(amount, rounded), RawAmount = raw,
			Factor = amount == 0 ? 0 : raw / amount, RuleType = rule.RuleType, RoundingMode = rule.RoundingMode,
			Explanation = $"Monthly cutoff evaluated across {periods.Count} service months; resulting head charge allocated using the payment term percentages." };
	}
}
