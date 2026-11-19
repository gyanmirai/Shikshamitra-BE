using System;
using System.Collections.Generic;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Collections;

public enum CollectionAllocationComponent { Principal, LateFee, Charge }

public sealed record CollectionAllocationTarget(Guid Id, CollectionAllocationComponent Component, decimal Outstanding);
public sealed record CollectionAllocatedAmount(Guid Id, CollectionAllocationComponent Component, decimal Amount);

/// <summary>Allocates decimal currency against the same ordered targets displayed by collection.</summary>
public static class SequentialCollectionAllocation
{
	public static IReadOnlyList<CollectionAllocatedAmount> Allocate(
		IReadOnlyList<CollectionAllocationTarget> orderedTargets, decimal received)
	{
		if (received <= 0m || decimal.Round(received, 2) != received)
			throw new BusinessException(FeeCollectionErrorCodes.PaymentAmountMustBePositive);
		var remaining = received;
		var result = new List<CollectionAllocatedAmount>();
		var seen = new HashSet<(Guid, CollectionAllocationComponent)>();
		foreach (var target in orderedTargets)
		{
			if (target.Id == Guid.Empty || target.Outstanding < 0m || !seen.Add((target.Id, target.Component)))
				throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
			var amount = Math.Min(remaining, target.Outstanding);
			if (amount > 0m) result.Add(new(target.Id, target.Component, amount));
			remaining -= amount;
		}
		if (remaining > 0m) throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding);
		return result;
	}
}
