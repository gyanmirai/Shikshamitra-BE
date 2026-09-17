using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class ConcessionAmountAllocator_Tests
{
	[Fact]
	public void Proportional_allocation_is_exact_bounded_and_order_independent()
	{
		var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
		var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
		var forward = new[]
		{
			new ConcessionAmountCapacity(first, 8000m),
			new ConcessionAmountCapacity(second, 2000m),
		};

		var a = ConcessionAmountAllocator.AllocateProportionally(forward, 1000m);
		var b = ConcessionAmountAllocator.AllocateProportionally(
			forward.OrderByDescending(x => x.Key).ToArray(), 1000m);

		Assert.Equal(a, b);
		Assert.Equal(1000m, a.Sum(x => x.Amount));
		Assert.Equal(800m, a.Single(x => x.Key == first).Amount);
		Assert.Equal(200m, a.Single(x => x.Key == second).Amount);
	}

	[Fact]
	public void Minor_unit_remainder_never_exceeds_line_capacity()
	{
		var inputs = Enumerable.Range(1, 3)
			.Select(i => new ConcessionAmountCapacity(new Guid(i, 0, 0, new byte[8]), 0.01m))
			.ToArray();

		var result = ConcessionAmountAllocator.AllocateProportionally(inputs, 0.02m);

		Assert.Equal(0.02m, result.Sum(x => x.Amount));
		Assert.All(result, x => Assert.InRange(x.Amount, 0m, 0.01m));
	}
}
