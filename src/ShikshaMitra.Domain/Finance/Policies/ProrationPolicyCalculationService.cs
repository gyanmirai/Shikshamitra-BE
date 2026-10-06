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
			// A null withdrawal model identifies an immutable legacy snapshot.
			// Preserve its original single-boundary calculation.
			factor = rule.EarlyWithdrawalRuleType.HasValue || !rule.ApplyOnAdmission ||
				!rule.ApplyOnEarlyWithdrawal || rule.RuleType == ProrationRuleType.FullMonthCharge
				? CalculateEventFactor(rule, coverageStart, coverageEnd, chargeStart, chargeEnd, out chargedDays)
				: rule.RuleType switch
			{
				ProrationRuleType.FullCharge => 1m,
				ProrationRuleType.Daily => CalculateDailyFactor(actualDays, coverageDays, rule.MinimumChargeDays, out chargedDays),
				ProrationRuleType.MonthlyCutoff => CalculateMonthlyFactor(
					eligibleStart, rule.CutoffDay, rule.AfterCutoffAction),
				_ => throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule)
			};
		}

		var raw = Math.Min(scheduledAmount, Math.Max(0m, scheduledAmount * factor));
		var amount = ShikshaMitra.Finance.WholeRupeeAmounts.RoundUp(raw);
		return new ProrationCalculationResult(
			scheduledAmount, amount, raw, factor, coverageStart, coverageEnd,
			actualDays == 0 ? null : eligibleStart,
			actualDays == 0 ? null : eligibleEnd,
			coverageDays, actualDays, chargedDays, rule.RuleType, ProrationRoundingMode.RoundUpRupee,
			BuildExplanation(rule, actualDays, chargedDays, coverageDays, factor));
	}

	private static decimal CalculateEventFactor(
		ProrationPolicyRuleSnapshot rule, DateTime coverageStart, DateTime coverageEnd,
		DateTime chargeStart, DateTime chargeEnd, out int chargedDays)
	{
		var admission = rule.ApplyOnAdmission ? rule.RuleType : ProrationRuleType.FullCharge;
		var withdrawal = rule.ApplyOnEarlyWithdrawal
			? rule.EarlyWithdrawalRuleType ?? (rule.RuleType == ProrationRuleType.MonthlyCutoff ? ProrationRuleType.FullMonthCharge : rule.RuleType)
			: ProrationRuleType.FullCharge;
		if (!Enum.IsDefined(admission) || !Enum.IsDefined(withdrawal))
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
		var start = admission == ProrationRuleType.FullCharge || chargeStart < coverageStart ? coverageStart : chargeStart;
		var end = withdrawal == ProrationRuleType.FullCharge || chargeEnd > coverageEnd ? coverageEnd : chargeEnd;
		var coverageDays = (coverageEnd - coverageStart).Days + 1;
		var minimumDays = Math.Max(admission == ProrationRuleType.Daily ? rule.MinimumChargeDays ?? 0 : 0,
			withdrawal == ProrationRuleType.Daily ? rule.EarlyWithdrawalMinimumChargeDays ??
				(rule.EarlyWithdrawalRuleType.HasValue ? 0 : rule.MinimumChargeDays ?? 0) : 0);
		if (minimumDays is < 0 or > 366)
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
		chargedDays = Math.Min(coverageDays, Math.Max((end - start).Days + 1, minimumDays));
		if (admission is not (ProrationRuleType.MonthlyCutoff or ProrationRuleType.FullMonthCharge) &&
			withdrawal is not (ProrationRuleType.MonthlyCutoff or ProrationRuleType.FullMonthCharge))
			return chargedDays / (decimal)coverageDays;

		// Equal service-month shares, independent of payment-term installments.
		var monthCount = (coverageEnd.Year - coverageStart.Year) * 12 + coverageEnd.Month - coverageStart.Month + 1;
		decimal total = 0m;
		for (var i = 0; i < monthCount; i++)
		{
			var month = new DateTime(coverageStart.Year, coverageStart.Month, 1).AddMonths(i);
			var monthEnd = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
			var periodStart = month < coverageStart ? coverageStart : month;
			var periodEnd = monthEnd > coverageEnd ? coverageEnd : monthEnd;
			if (start > periodEnd || end < periodStart) continue;
			var dailyStart = admission == ProrationRuleType.Daily && start > periodStart ? start : periodStart;
			var dailyEnd = withdrawal == ProrationRuleType.Daily && end < periodEnd ? end : periodEnd;
			var fraction = ((dailyEnd - dailyStart).Days + 1) / (decimal)((periodEnd - periodStart).Days + 1);
			decimal cap = 1m;
			if (admission == ProrationRuleType.MonthlyCutoff && chargeStart > coverageStart &&
				chargeStart >= month && chargeStart <= monthEnd)
				cap = CalculateMonthlyFactor(chargeStart, rule.CutoffDay, rule.AfterCutoffAction);
			if (withdrawal == ProrationRuleType.MonthlyCutoff && chargeEnd < coverageEnd &&
				chargeEnd >= month && chargeEnd <= monthEnd)
			{
				if (rule.EarlyWithdrawalCutoffDay is null or < 1 or > 31)
					throw new BusinessException(ProrationPolicyErrorCodes.InvalidRule);
				var cutoff = Math.Min(rule.EarlyWithdrawalCutoffDay.Value, DateTime.DaysInMonth(month.Year, month.Month));
				if (chargeEnd.Day <= cutoff)
					cap = Math.Min(cap, rule.EarlyWithdrawalBeforeCutoffAction == ProrationAfterCutoffAction.HalfCharge ? .5m : 0m);
			}
			// In the same month the strongest reduction applies once.
			total += Math.Min(fraction, cap) / monthCount;
		}
		return Math.Min(1m, Math.Max(total, minimumDays / (decimal)coverageDays));
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

	private static decimal Round(decimal value, ProrationRoundingMode mode) =>
		ShikshaMitra.Finance.WholeRupeeAmounts.RoundUp(value);

	private static string BuildExplanation(
		ProrationPolicyRuleSnapshot rule,
		int actualDays,
		int chargedDays,
		int coverageDays,
		decimal factor) => rule.EarlyWithdrawalRuleType.HasValue
		? $"Admission: {(rule.ApplyOnAdmission ? rule.RuleType : ProrationRuleType.FullCharge)}; early withdrawal: {(rule.ApplyOnEarlyWithdrawal ? rule.EarlyWithdrawalRuleType : ProrationRuleType.FullCharge)}. Inclusive calendar dates; factor {factor:0.####}; rounding applied once."
		: rule.RuleType switch
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
