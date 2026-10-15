using System.Text.Json.Serialization;

namespace ShikshaMitra.Finance.Accounts.Settings;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountHeadType
{
	Asset = 1,
	Liability = 2,
	Equity = 3,
	Income = 4,
	Expense = 5
}

public static class AccountSettingConsts
{
	public const int MaxCodeLength = 32;
	public const int MaxNameLength = 128;
	public const int MaxIncomeCategoryNameLength = 40;
	public const int MaxBankNameLength = 100;
	public const int MaxBankPurposeLength = 500;
	public const int MaxDescriptionLength = 1000;
	public const int MaxDeactivationReasonLength = 500;
	public const int MaxCurrencyCodeLength = 3;

	public static string NormalizeCode(string value) =>
		(value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "-");

	public static string NormalizeName(string value) =>
		(value ?? string.Empty).Trim().ToUpperInvariant();
}
