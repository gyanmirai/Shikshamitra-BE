using System;
using System.Globalization;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static class FinanceMoneyPrecision
{
	public const decimal MaximumStoredAmount = 9999999999999999.99m;
	public static decimal FromLegacyDouble(double value)
	{
		if (!double.IsFinite(value)) throw Invalid();
		if (!decimal.TryParse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float,
			CultureInfo.InvariantCulture, out var amount)) throw Invalid();
		Ensure(amount);
		if ((double)amount != value) throw Invalid();
		return amount;
	}
	public static double ToLegacyDouble(decimal amount)
	{
		Ensure(amount);
		var value = (double)amount;
		if (FromLegacyDouble(value) != amount) throw Invalid();
		return value;
	}
	public static void Ensure(decimal amount, bool positive = false)
	{
		if (amount < 0m || positive && amount == 0m || amount > MaximumStoredAmount || decimal.Round(amount, 2) != amount)
			throw Invalid();
	}
	private static BusinessException Invalid() => new(FinanceMoneyErrorCodes.ReviewRequired,
		"The amount cannot be represented exactly in whole paise. Review the original evidence.");
}
