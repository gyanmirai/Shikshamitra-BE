namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>Input bounds for collection policy configuration and its interactive simulation.</summary>
public static class CollectionPolicyNumericLimits
{
	public const int MaximumDays = 365;
	public const decimal MaximumAmount = 999999m;
	public const decimal MinimumValue = 0.01m;
	public const decimal MaximumPercentage = 100m;

	public static bool IsAmount(decimal value) =>
		value >= MinimumValue && value <= MaximumAmount && HasCurrencyPrecision(value);

	public static bool IsPercentage(decimal value) =>
		value >= MinimumValue && value <= MaximumPercentage && HasCurrencyPrecision(value);

	public static bool HasCurrencyPrecision(decimal value) => decimal.Round(value, 2) == value;
}
