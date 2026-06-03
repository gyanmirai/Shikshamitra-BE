using System;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollProrationCalculator
{
	public static decimal CalculatePayableDays(int presentDays, int halfWorkingDays) =>
		presentDays + 0.5m * halfWorkingDays;

	public static decimal CalculateProrationFactor(decimal basisDays, decimal payableDays)
	{
		if (basisDays <= 0)
		{
			return 0m;
		}

		return Math.Min(1m, payableDays / basisDays);
	}

	public static decimal ProrateAmount(decimal amount, bool isTemp, decimal factor)
	{
		if (isTemp)
		{
			return amount;
		}

		return Math.Round(amount * factor, 2, MidpointRounding.AwayFromZero);
	}
}
