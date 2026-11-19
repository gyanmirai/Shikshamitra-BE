using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>Attributes each cent to exactly one category line and payment instrument.</summary>
public static class FinanceIncomeAllocator
{
    public sealed record Allocation(int InstrumentIndex, int CategoryIndex, decimal Amount);

    public static IReadOnlyList<Allocation> Allocate(IReadOnlyList<decimal> categories,
        IReadOnlyList<decimal> instruments)
    {
        if (categories.Count == 0 || instruments.Count == 0 ||
            categories.Concat(instruments).Any(x => x <= 0 || decimal.Round(x, 2) != x) ||
            categories.Sum() != instruments.Sum())
            throw new BusinessException("Finance:Routing:AllocationMismatch");

        var remaining = categories.ToArray();
        var categoryIndex = 0;
        var allocations = new List<Allocation>();
        for (var instrumentIndex = 0; instrumentIndex < instruments.Count; instrumentIndex++)
        {
            var amount = instruments[instrumentIndex];
            while (amount > 0)
            {
                var allocated = Math.Min(amount, remaining[categoryIndex]);
                allocations.Add(new(instrumentIndex, categoryIndex, allocated));
                amount -= allocated;
                remaining[categoryIndex] -= allocated;
                if (remaining[categoryIndex] == 0) categoryIndex++;
            }
        }
        return allocations;
    }
}
