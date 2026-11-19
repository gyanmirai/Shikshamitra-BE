using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance;

/// <summary>Whole-rupee money rules. Splits truncate each share and put the remainder on the final line.</summary>
public static class WholeRupeeAmounts
{
	// Remove division noise (for example 3000 * (5 / 30)) before applying ceiling.
	// Twelve fractional digits retain far more precision than money inputs permit.
	public static decimal RoundUp(decimal amount) => decimal.Ceiling(decimal.Round(amount, 12, MidpointRounding.AwayFromZero));
	public static decimal? RoundUp(decimal? amount) => amount.HasValue ? RoundUp(amount.Value) : null;

	// Never exceed an eligible balance or a hard policy cap, including historical fractional balances.
	public static decimal RoundUpWithin(decimal amount, decimal maximum) =>
		Math.Max(0m, Math.Min(RoundUp(amount), decimal.Floor(maximum)));

	public static decimal[] Allocate(decimal amount, IReadOnlyList<decimal> percentages)
	{
		var total = RoundUp(amount);
		var shares = percentages.Select(p => decimal.Floor(total * p / 100m)).ToArray();
		if (shares.Length > 0) shares[^1] = total - shares.Take(shares.Length - 1).Sum();
		return shares;
	}
}
