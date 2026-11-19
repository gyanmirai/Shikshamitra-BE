using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

/// <summary>Allocate one recalculated head entitlement, never prorate an already reduced installment.</summary>
public static class WithdrawalProrationAllocation
{
	public static decimal[] Credits(IReadOnlyList<decimal> original, IReadOnlyList<decimal> otherCredits,
		IReadOnlyList<decimal> percentages, decimal revisedHeadAmount, IReadOnlyList<decimal>? netPaid = null)
	{
		if (original.Count != otherCredits.Count || original.Count != percentages.Count ||
			original.Any(x => x < 0) || otherCredits.Where((x, i) => x < 0 || x > original[i]).Any() ||
			netPaid != null && (netPaid.Count != original.Count || netPaid.Any(x => x < 0)))
			throw new BusinessException(ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid);
		// Validate the immutable installment weights, but compute entitlement only at head level.
		_ = AssignmentAmountCalculator.Allocate(revisedHeadAmount, percentages);
		var capacities = original.Select((x, i) => Math.Max(0m, x - otherCredits[i])).ToArray();
		var required = Math.Min(Math.Max(0m, original.Sum() - revisedHeadAmount), capacities.Sum());
		var result = new decimal[original.Count];
		var remaining = required;
		// Cancel unpaid principal first, latest installment first. Only then reduce paid principal.
		// This avoids artificial arrears on one installment while another holds an excess payment.
		for (var pass = 0; pass < 2; pass++)
		{
			for (var i = result.Length - 1; i >= 0 && remaining > 0m; i--)
			{
				var available = pass == 0 ? Math.Max(0m, capacities[i] - (netPaid?[i] ?? 0m)) : capacities[i] - result[i];
				var credit = Math.Min(remaining, available);
				result[i] += credit;
				remaining -= credit;
			}
		}
		if (remaining != 0m) throw new BusinessException(ProrationPolicyErrorCodes.AdjustmentConflict);
		return result;
	}
}
