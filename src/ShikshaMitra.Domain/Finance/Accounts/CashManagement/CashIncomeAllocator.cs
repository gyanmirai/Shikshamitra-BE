using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

/// <summary>Allocates a net deposit proportionally, preserving every paise and stable input order for ties.</summary>
public static class CashIncomeAllocator
{
	public static decimal[] Allocate(IReadOnlyList<decimal> grossAmounts, decimal depositedAmount)
	{
		if (grossAmounts.Count == 0 || grossAmounts.Any(x => x <= 0 || decimal.Round(x, 2) != x) ||
			depositedAmount < 0 || decimal.Round(depositedAmount, 2) != depositedAmount || depositedAmount > grossAmounts.Sum())
			throw new BusinessException(CashManagementErrorCodes.InvalidAmount);
		var gross = grossAmounts.Sum();
		var exactCents = grossAmounts.Select(x => depositedAmount * 100m * x / gross).ToArray();
		var cents = exactCents.Select(decimal.Floor).ToArray();
		var remainingCents = (int)(depositedAmount * 100m - cents.Sum());
		foreach (var index in Enumerable.Range(0, cents.Length)
			.OrderByDescending(i => exactCents[i] - cents[i]).ThenBy(i => i).Take(remainingCents))
			cents[index]++;
		return cents.Select(x => x / 100m).ToArray();
	}
}
