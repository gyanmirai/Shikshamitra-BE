using System;
using System.Linq;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class WholeRupeeAmounts_Tests
{
	[Theory]
	[InlineData(100.01, 101)]
	[InlineData(100.99, 101)]
	[InlineData(100, 100)]
	[InlineData(0, 0)]
	public void Money_rounds_up_without_changing_whole_rupees(decimal input, decimal expected) =>
		Assert.Equal(expected, WholeRupeeAmounts.RoundUp(input));

	[Fact]
	public void Repeating_day_fractions_do_not_add_an_extra_rupee_to_exact_charges()
	{
		Assert.Equal(500m, WholeRupeeAmounts.RoundUp(3000m * (5m / 30m)));
		Assert.Equal(1m, WholeRupeeAmounts.RoundUp(.01m / 365m));
	}

	[Fact]
	public void Installment_residual_belongs_to_the_last_installment()
	{
		Assert.Equal(new[] { 333m, 333m, 334m }, WholeRupeeAmounts.Allocate(1000m, [33.33m, 33.33m, 33.34m]));
		Assert.Equal(new[] { 333m, 333m, 335m }, WholeRupeeAmounts.Allocate(1000.01m, [33.33m, 33.33m, 33.34m]));
	}

	[Fact]
	public void Small_and_fractional_totals_never_create_negative_or_fractional_installments()
	{
		for (var cents = 0; cents <= 10001; cents += 7)
		{
			var total = cents / 100m;
			var shares = WholeRupeeAmounts.Allocate(total, [12.5m, 12.5m, 25m, 25m, 25m]);
			Assert.Equal(decimal.Ceiling(total), shares.Sum());
			Assert.All(shares, amount => Assert.True(amount >= 0 && amount == decimal.Truncate(amount)));
		}
	}

	[Theory]
	[InlineData(10.01, 100, 11)]
	[InlineData(10.01, 10.20, 10)]
	[InlineData(0.40, 0.40, 0)]
	[InlineData(100.01, 99.99, 99)]
	public void Caps_and_eligible_balances_are_never_exceeded(decimal amount, decimal cap, decimal expected) =>
		Assert.Equal(expected, WholeRupeeAmounts.RoundUpWithin(amount, cap));
}
