using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public sealed record ConcessionAmountCapacity(Guid Key, decimal Capacity);
public sealed record ConcessionAmountShare(Guid Key, decimal Amount);

/// <summary>
/// Allocates a request-level amount in currency minor units. Results are stable for
/// the same keys and capacities, independent of caller ordering.
/// </summary>
public static class ConcessionAmountAllocator
{
	private const decimal MinorUnitFactor = 100m;

	public static IReadOnlyList<ConcessionAmountShare> AllocateProportionally(
		IReadOnlyCollection<ConcessionAmountCapacity> inputs,
		decimal requestedTotal)
	{
		if (inputs.Count == 0 || inputs.Any(x => x.Key == Guid.Empty || x.Capacity < 0m))
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);
		if (inputs.Select(x => x.Key).Distinct().Count() != inputs.Count)
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);

		var ordered = inputs.OrderBy(x => x.Key).ToList();
		var capacities = ordered.ToDictionary(x => x.Key, x => ToMinorUnits(x.Capacity));
		var capacityTotal = capacities.Values.Sum();
		if (capacityTotal <= 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);

		var target = Math.Min(Math.Max(0L, ToMinorUnits(requestedTotal)), capacityTotal);
		if (target <= 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);

		var candidates = ordered.Select(item =>
		{
			var capacity = capacities[item.Key];
			var exact = (decimal)target * capacity / capacityTotal;
			var floor = Math.Min(capacity, decimal.ToInt64(decimal.Floor(exact)));
			return new AllocationCandidate(item.Key, capacity, floor, exact - floor);
		}).ToList();

		var remaining = target - candidates.Sum(x => x.Allocated);
		while (remaining > 0)
		{
			var eligible = candidates
				.Where(x => x.Allocated < x.Capacity)
				.OrderByDescending(x => x.Remainder)
				.ThenBy(x => x.Key)
				.ToList();
			if (eligible.Count == 0)
				throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);

			foreach (var item in eligible)
			{
				if (remaining == 0) break;
				item.Allocated++;
				remaining--;
			}
		}

		return candidates
			.OrderBy(x => x.Key)
			.Select(x => new ConcessionAmountShare(x.Key, x.Allocated / MinorUnitFactor))
			.ToList();
	}

	private static long ToMinorUnits(decimal amount) =>
		decimal.ToInt64(decimal.Round(amount * MinorUnitFactor, 0, MidpointRounding.AwayFromZero));

	private sealed class AllocationCandidate(Guid key, long capacity, long allocated, decimal remainder)
	{
		public Guid Key { get; } = key;
		public long Capacity { get; } = capacity;
		public long Allocated { get; set; } = allocated;
		public decimal Remainder { get; } = remainder;
	}
}
