using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// One authoritative implementation of the late-fee math, per the algorithm in
/// collection-policy-flow.md §5, extended with the 7-step pipeline from
/// collection-policy-implementation-plan.md Phase 2. Called identically from a real
/// collection, an impact preview, and the /simulate endpoint — never re-implemented
/// per caller — so they can never drift (implementation plan §2, "single source of
/// truth for calculation").
/// </summary>
public class LateFeeCalculationService : ITransientDependency
{
	public LateFeeCalculationResult Calculate(CollectionPolicy policy, LateFeeCalculationInput input)
	{
		// Step 1 — student-level exemption short-circuits everything else.
		if (input.IsLateFeeExempt)
		{
			return LateFeeCalculationResult.Exempt("Student is marked late-fee exempt (StudentFeePolicyOverride.isLateFeeExempt).");
		}

		if (!policy.EnableLateFee)
		{
			return LateFeeCalculationResult.Exempt("Late fee is disabled on this policy.");
		}

		if (input.FeeHeadId is { } scopedFeeHeadId && !policy.AppliesLateFeeTo(scopedFeeHeadId))
		{
			return LateFeeCalculationResult.Exempt("This fee head is outside the policy's late-fee scope.");
		}

		// Step 2 — exception windows (CollectionPolicyExceptionWindow, Phase 4j). The active
		// window (if any) is resolved by ExceptionWindowResolver — a DB-touching domain service —
		// before this method is called, so Calculate itself stays a pure function. SuspendLateFeeAccrual
		// short-circuits to zero; ExtendGracePeriod adds its extensionDays into step 3's grace-period
		// math; DisableOnlineCollection/Custom don't affect late-fee math (payment-method availability
		// and free-form markers respectively) so they fall through unchanged.
		string? exceptionWindowApplied = null;
		var exceptionGracePeriodExtensionDays = 0;

		if (input.ActiveExceptionWindowEffect == ExceptionWindowEffect.SuspendLateFeeAccrual)
		{
			var label = input.ExceptionWindowLabel ?? "an active exception window";
			return new LateFeeCalculationResult(
				0, false, 0, policy.GracePeriodDays, label, "Inherit", 0, null, "None",
				$"Late fee accrual suspended by {label} — no late fee applies while this window is active.");
		}

		if (input.ActiveExceptionWindowEffect == ExceptionWindowEffect.ExtendGracePeriod)
		{
			exceptionWindowApplied = input.ExceptionWindowLabel ?? "ExtendGracePeriod";
			exceptionGracePeriodExtensionDays = input.ExceptionWindowExtensionDays ?? 0;
		}
		else if (input.ActiveExceptionWindowEffect is not null)
		{
			exceptionWindowApplied = input.ExceptionWindowLabel;
		}

		// Step 3 — grace-period offset: student override wins if set, else the policy's own
		// gracePeriodDays, plus any exception-window extension from step 2. All day counts
		// use calendar days, including weekends and holidays.
		var effectiveGracePeriodDays =
			(input.CustomGracePeriodDays ?? policy.GracePeriodDays) + exceptionGracePeriodExtensionDays;

		var effectiveDaysLate = input.DaysLate - effectiveGracePeriodDays;
		if (effectiveDaysLate <= 0)
		{
			return new LateFeeCalculationResult(
				0, false, 0, effectiveGracePeriodDays, exceptionWindowApplied, "Inherit", 0, null, "None",
				$"Within grace period ({effectiveGracePeriodDays} days) — no late fee yet.");
		}

		// Step 4 — per-fee-head override (CollectionPolicyFeeHeadOverride, Phase 4a). NoLateFee
		// short-circuits to zero for this fee head; CustomRule swaps in the fee-head-level
		// LateFeeRule for the remaining steps; Inherit (or no override row / no FeeHeadId) proceeds
		// with the policy-level rule.
		var feeHeadOverrideApplied = "Inherit";
		var effectiveRule = EffectiveLateFeeRule.FromPolicy(policy);

		if (input.FeeHeadId is { } feeHeadId && policy.TryGetFeeHeadOverride(feeHeadId, out var feeHeadOverride) && feeHeadOverride is not null)
		{
			if (feeHeadOverride.OverrideMode == FeeHeadOverrideMode.NoLateFee)
			{
				return new LateFeeCalculationResult(
					0, false, effectiveDaysLate, effectiveGracePeriodDays, exceptionWindowApplied, "NoLateFee", 0, null, "None",
					"Fee head override: NoLateFee — no late fee applies to this fee head.");
			}

			if (feeHeadOverride.OverrideMode == FeeHeadOverrideMode.CustomRule && feeHeadOverride.LateFeeRule is not null)
			{
				effectiveRule = EffectiveLateFeeRule.FromOverride(feeHeadOverride.LateFeeRule);
				feeHeadOverrideApplied = "CustomRule";
			}
		}

		// Step 6's day-cutoff is applied here, before step 5's math, so every calculation type
		// (PerDay/PerWeek/PerMonth/Compound/Slab) naturally stops growing once the cutoff binds —
		// "freeze late fee after 90 days" (Collection_Policy_Design_Recommendations.md §2).
		var cutoffDays = effectiveRule.MaximumChargingDays;
		var cappedByDaysCutoff = false;
		if (cutoffDays.HasValue && effectiveDaysLate > cutoffDays.Value)
		{
			effectiveDaysLate = cutoffDays.Value;
			cappedByDaysCutoff = true;
		}

		// Step 5 — the 7 calculation types.
		var rawAmount = CalculateRaw(effectiveRule, effectiveDaysLate, input);

		// Step 6 — maximumLateFeeAmount cap (amount-based, independent of the day cutoff above).
		var amountBeforeCap = rawAmount;
		string? cappedBy = cappedByDaysCutoff ? "maximumChargingDays" : null;
		var cappedAmount = rawAmount;
		if (effectiveRule.MaximumLateFeeAmount is { } maxAmount && cappedAmount > maxAmount)
		{
			cappedAmount = maxAmount;
			cappedBy = "maximumLateFeeAmount";
		}

		// Step 7 — rounding.
		var finalAmount = ShikshaMitra.Finance.WholeRupeeAmounts.RoundUpWithin(cappedAmount, effectiveRule.MaximumLateFeeAmount ?? decimal.MaxValue);

		var explanation = BuildExplanation(effectiveRule, effectiveDaysLate, finalAmount, cappedBy);
		if (feeHeadOverrideApplied == "CustomRule")
		{
			explanation = $"Fee head override (CustomRule) — {explanation}";
		}

		return new LateFeeCalculationResult(
			finalAmount,
			false,
			effectiveDaysLate,
			effectiveGracePeriodDays,
			exceptionWindowApplied,
			feeHeadOverrideApplied,
			amountBeforeCap,
			cappedBy,
			RoundingMethodType.RoundUp.ToString(),
			explanation);
	}

	private static decimal CalculateRaw(EffectiveLateFeeRule rule, int effectiveDaysLate, LateFeeCalculationInput input)
	{
		var baseAmount = ResolveBase(rule.PercentageBase, input);

		return rule.LateFeeType switch
		{
			LateFeeCalculationType.Flat => rule.FlatAmount ?? 0,
			LateFeeCalculationType.Percentage => (rule.Percentage ?? 0) / 100m * baseAmount,
			LateFeeCalculationType.PerDay => (rule.AmountPerDay ?? 0) * effectiveDaysLate,
			LateFeeCalculationType.PerWeek => (rule.AmountPerWeek ?? 0) * CeilDiv(effectiveDaysLate, 7),
			LateFeeCalculationType.PerMonth => (rule.AmountPerMonth ?? 0) * CeilDiv(effectiveDaysLate, 30),
			LateFeeCalculationType.Compound => CalculateCompound(rule, effectiveDaysLate, baseAmount),
			LateFeeCalculationType.Slab => CalculateSlab(rule, effectiveDaysLate, baseAmount),
			_ => 0
		};
	}

	private static decimal ResolveBase(PercentageBaseType percentageBase, LateFeeCalculationInput input) =>
		percentageBase switch
		{
			PercentageBaseType.OriginalFee => input.OriginalFeeAmount,
			PercentageBaseType.RemainingBalance => input.RemainingBalance,
			_ => input.OutstandingAmount
		};

	private static decimal CalculateCompound(EffectiveLateFeeRule rule, int effectiveDaysLate, decimal baseAmount)
	{
		var periods = rule.CompoundFrequency switch
		{
			CompoundFrequencyType.Weekly => effectiveDaysLate / 7,
			CompoundFrequencyType.Monthly => effectiveDaysLate / 30,
			_ => effectiveDaysLate
		};

		var compounded = baseAmount;
		var rate = (rule.Percentage ?? 0) / 100m;
		for (var i = 0; i < periods; i++)
		{
			compounded += compounded * rate;
		}

		return compounded - baseAmount;
	}

	private static decimal CalculateSlab(EffectiveLateFeeRule rule, int effectiveDaysLate, decimal baseAmount)
	{
		decimal total = 0;
		foreach (var slab in rule.EscalationRules)
		{
			if (effectiveDaysLate < slab.StartDay)
			{
				continue;
			}

			var limitDay = slab.EndDay is { } endDay ? Math.Min(effectiveDaysLate, endDay) : effectiveDaysLate;
			var activeDays = limitDay - slab.StartDay + 1;
			if (activeDays <= 0)
			{
				continue;
			}

			total += slab.CalculationType == EscalationCalculationType.Flat
				? slab.Value * activeDays
				: slab.Value / 100m * baseAmount * activeDays;
		}

		return total;
	}

	private static decimal ApplyRounding(decimal amount, RoundingMethodType method) =>
		ShikshaMitra.Finance.WholeRupeeAmounts.RoundUp(amount);

	private static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;

	private static string BuildExplanation(EffectiveLateFeeRule rule, int effectiveDaysLate, decimal finalAmount, string? cappedBy)
	{
		var basis = rule.LateFeeType switch
		{
			LateFeeCalculationType.PerDay => $"₹{rule.AmountPerDay}/day × {effectiveDaysLate} days late",
			LateFeeCalculationType.PerWeek => $"₹{rule.AmountPerWeek}/week × {CeilDiv(effectiveDaysLate, 7)} weeks late",
			LateFeeCalculationType.PerMonth => $"₹{rule.AmountPerMonth}/month × {CeilDiv(effectiveDaysLate, 30)} months late",
			LateFeeCalculationType.Flat => $"flat ₹{rule.FlatAmount}",
			LateFeeCalculationType.Percentage => $"{rule.Percentage}% of {rule.PercentageBase}",
			LateFeeCalculationType.Compound => $"{rule.Percentage}% compounded {rule.CompoundFrequency} over {effectiveDaysLate} days",
			LateFeeCalculationType.Slab => $"slab-based over {effectiveDaysLate} days late",
			_ => "unspecified"
		};

		var capNote = cappedBy is null ? "" : $" (capped by {cappedBy})";
		return $"{basis} = ₹{finalAmount}{capNote}";
	}

	/// <summary>
	/// The late-fee-relevant fields shared by CollectionPolicy (policy-level) and
	/// LateFeeRuleOverride (fee-head-level, Phase 4a) — lets the calculation steps below
	/// operate on "whichever rule is in effect for this fee head" without caring which one it is.
	/// </summary>
	private sealed record EffectiveLateFeeRule(
		LateFeeCalculationType LateFeeType,
		decimal? FlatAmount,
		decimal? Percentage,
		PercentageBaseType PercentageBase,
		decimal? AmountPerDay,
		decimal? AmountPerWeek,
		decimal? AmountPerMonth,
		CompoundFrequencyType CompoundFrequency,
		IReadOnlyList<CollectionPolicyEscalationRule> EscalationRules,
		RoundingMethodType RoundingMethod,
		decimal? MaximumLateFeeAmount,
		int? MaximumChargingDays)
	{
		public static EffectiveLateFeeRule FromPolicy(CollectionPolicy policy) => new(
			policy.LateFeeType, policy.FlatAmount, policy.Percentage, policy.PercentageBase,
			policy.AmountPerDay, policy.AmountPerWeek, policy.AmountPerMonth, policy.CompoundFrequency,
			policy.GetEscalationRules(), policy.RoundingMethod, policy.MaximumLateFeeAmount, policy.MaximumChargingDays);

		public static EffectiveLateFeeRule FromOverride(LateFeeRuleOverride rule) => new(
			rule.LateFeeType, rule.FlatAmount, rule.Percentage, rule.PercentageBase,
			rule.AmountPerDay, rule.AmountPerWeek, rule.AmountPerMonth, rule.CompoundFrequency,
			rule.EscalationRules, rule.RoundingMethod, rule.MaximumLateFeeAmount, rule.MaximumChargingDays);
	}
}
