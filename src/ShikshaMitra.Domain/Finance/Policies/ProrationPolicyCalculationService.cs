using System;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ProrationPolicyCalculationService : DomainService
{
	public ProrationCalculationResult Calculate(
		ProrationPolicyRuleSnapshot rule,
		decimal scheduledAmount,
		DateTime coverageStart,
		DateTime coverageEnd,
		DateTime chargeStart,
		DateTime chargeEnd)
	{
		coverageStart = coverageStart.Date;
		coverageEnd = coverageEnd.Date;
		chargeStart = chargeStart.Date;
		chargeEnd = chargeEnd.Date;
		if (scheduledAmount < 0m || coverageStart > coverageEnd || chargeStart > chargeEnd)
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidChargePeriod);

		var eligibleStart = chargeStart > coverageStart ? chargeStart : coverageStart;
		var eligibleEnd = chargeEnd < coverageEnd ? chargeEnd : coverageEnd;
		var coverageDays = (coverageEnd - coverageStart).Days + 1;
		var actualDays = eligibleStart <= eligibleEnd ? (eligibleEnd - eligibleStart).Days + 1 : 0;
		decimal factor;
		var chargedDays = actualDays;

		if (actualDays == 0)
		{
			factor = 0m;
		}
		else
		{
			factor = rule.RuleType switch
			{
				ProrationRuleType.FullCharge => 1m,
				ProrationRuleType.Daily => CalculateDailyFactor(actualDays, coverageDays, rule.MinimumChargeDays, out chargedDays),
				ProrationRuleType.MonthlyCutoff => CalculateMonthlyFactor(
					eligibleStart, rule.CutoffDay, rule.AfterCutoffAction),
				_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule)
			};
		}

		var raw = Math.Min(scheduledAmount, Math.Max(0m, scheduledAmount * factor));
		var amount = Math.Min(scheduledAmount, Round(raw, rule.RoundingMode));
		return new ProrationCalculationResult(
			scheduledAmount, amount, raw, factor, coverageStart, coverageEnd,
			actualDays == 0 ? null : eligibleStart,
			actualDays == 0 ? null : eligibleEnd,
			coverageDays, actualDays, chargedDays, rule.RuleType, rule.RoundingMode,
			BuildExplanation(rule, actualDays, chargedDays, coverageDays, factor));
	}

	private static decimal CalculateDailyFactor(int actualDays, int coverageDays, int? minimumChargeDays, out int chargedDays)
	{
		chargedDays = Math.Min(coverageDays, Math.Max(actualDays, minimumChargeDays ?? 0));
		return chargedDays / (decimal)coverageDays;
	}

	private static decimal CalculateMonthlyFactor(
		DateTime eligibleStart,
		int? cutoffDay,
		ProrationAfterCutoffAction? afterCutoffAction)
	{
		if (cutoffDay is null or < 1 or > 31)
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
		var resolvedCutoff = Math.Min(cutoffDay.Value, DateTime.DaysInMonth(eligibleStart.Year, eligibleStart.Month));
		if (eligibleStart.Day <= resolvedCutoff)
			return 1m;
		return afterCutoffAction == ProrationAfterCutoffAction.HalfCharge ? 0.5m : 0m;
	}

	private static decimal Round(decimal value, ProrationRoundingMode mode) => mode switch
	{
		ProrationRoundingMode.NearestRupee => Math.Round(value, 0, MidpointRounding.AwayFromZero),
		ProrationRoundingMode.RoundUpRupee => Math.Ceiling(value),
		ProrationRoundingMode.RoundDownRupee => Math.Floor(value),
		_ => Math.Round(value, 2, MidpointRounding.AwayFromZero)
	};

	private static string BuildExplanation(
		ProrationPolicyRuleSnapshot rule,
		int actualDays,
		int chargedDays,
		int coverageDays,
		decimal factor) => rule.RuleType switch
	{
		ProrationRuleType.Daily => $"{chargedDays} charged day(s) from {actualDays} eligible day(s) over {coverageDays} coverage day(s).",
		ProrationRuleType.MonthlyCutoff => $"Monthly cutoff rule applied with factor {factor:0.####}.",
		_ => actualDays == 0 ? "No overlap with the charge period." : "Full scheduled charge applies."
	};
}

public sealed record ProrationCalculationResult(
	decimal ScheduledAmount,
	decimal ProratedAmount,
	decimal RawAmount,
	decimal Factor,
	DateTime CoverageStartDate,
	DateTime CoverageEndDate,
	DateTime? EligibleStartDate,
	DateTime? EligibleEndDate,
	int CoverageDays,
	int ActualEligibleDays,
	int ChargedDays,
	ProrationRuleType RuleType,
	ProrationRoundingMode RoundingMode,
	string Explanation);
