using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Server-side mirror of the frontend's <c>computeSlabIssues</c> (useCollectionPolicyForm.ts /
/// CollectionCommon.tsx) — defense in depth beyond the client-side check, since the client
/// check can be bypassed by calling the API directly.
/// </summary>
public static class CollectionPolicySlabValidator
{
	public static void EnsureNoOverlapsOrGaps(IReadOnlyList<CollectionPolicyEscalationRule> rules, int? maximumChargingDays = null, bool validateInputBounds = true)
	{
		if (rules.Count == 0)
		{
			return;
		}

		var sorted = rules.OrderBy(r => r.StartDay).ToList();
		if (validateInputBounds)
		{
			foreach (var rule in sorted)
			{
				if (rule.StartDay is < 1 or > CollectionPolicyNumericLimits.MaximumDays ||
					rule.EndDay is < 1 or > CollectionPolicyNumericLimits.MaximumDays ||
					rule.EndDay < rule.StartDay ||
					maximumChargingDays.HasValue && (rule.StartDay > maximumChargingDays || rule.EndDay > maximumChargingDays) ||
					(rule.CalculationType == EscalationCalculationType.Percentage
						? !CollectionPolicyNumericLimits.IsPercentage(rule.Value)
						: rule.CalculationType != EscalationCalculationType.Flat || !CollectionPolicyNumericLimits.IsAmount(rule.Value)) ||
					rule.MaxFee.HasValue && !CollectionPolicyNumericLimits.IsAmount(rule.MaxFee.Value))
				{
					throw new BusinessException(CollectionPolicyErrorCodes.InvalidEscalationSlabs).WithData("reason", "invalid_range_or_value");
				}
			}
			// Slab days are chargeable days after grace, and an open final slab remains supported.
			if (sorted[0].StartDay != 1 || maximumChargingDays.HasValue &&
				sorted[^1].EndDay.HasValue && sorted[^1].EndDay < maximumChargingDays)
			{
				throw new BusinessException(CollectionPolicyErrorCodes.InvalidEscalationSlabs).WithData("reason", "gap");
			}
		}

		for (var i = 0; i < sorted.Count - 1; i++)
		{
			var current = sorted[i];
			var next = sorted[i + 1];

			if (current.EndDay is null || next.StartDay <= current.EndDay)
			{
				throw new BusinessException(CollectionPolicyErrorCodes.InvalidEscalationSlabs)
					.WithData("reason", "overlap")
					.WithData("day", next.StartDay);
			}

			if (next.StartDay > current.EndDay + 1)
			{
				throw new BusinessException(CollectionPolicyErrorCodes.InvalidEscalationSlabs)
					.WithData("reason", "gap")
					.WithData("afterDay", current.EndDay)
					.WithData("beforeDay", next.StartDay);
			}
		}
	}
}
