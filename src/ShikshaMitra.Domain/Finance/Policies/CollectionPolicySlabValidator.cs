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
	public static void EnsureNoOverlapsOrGaps(IReadOnlyList<CollectionPolicyEscalationRule> rules)
	{
		if (rules.Count < 2)
		{
			return;
		}

		var sorted = rules.OrderBy(r => r.StartDay).ToList();

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
