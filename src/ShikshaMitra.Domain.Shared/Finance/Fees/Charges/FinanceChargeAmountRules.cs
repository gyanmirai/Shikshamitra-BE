namespace ShikshaMitra.Finance.Fees.Charges;

public static class FinanceChargeAmountRules
{
	public const int DecimalPlaces = 2;
	public const decimal MaximumAmount = 9_999_999m;

	public static bool HasSupportedScale(decimal amount) =>
		decimal.Round(amount, DecimalPlaces) == amount;
}
