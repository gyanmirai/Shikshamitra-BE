using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace ShikshaMitra.Library.Catalogue;

public static class LibraryCataloguePriceRules
{
	public const decimal Maximum = 9999999999999999.99m;
	public static decimal? ResolveUpdate(decimal? current, decimal? legacyValue, string? exactText)
	{
		if (current.HasValue && (!legacyValue.HasValue || legacyValue.Value == current.Value) &&
			(exactText != null ? string.Equals(exactText, Format(current), StringComparison.Ordinal) : legacyValue.HasValue))
			return current;
		return Resolve(legacyValue, exactText);
	}

	public static decimal? Resolve(decimal? legacyValue, string? exactText)
	{
		if (legacyValue.HasValue) ValidateKnown(legacyValue.Value);
		if (exactText == null) return legacyValue;
		if (exactText.Length is < 1 or > 19 ||
			!Regex.IsMatch(exactText, @"\A[0-9]{1,16}(?:\.[0-9]{1,2})?\z", RegexOptions.CultureInvariant) ||
			!decimal.TryParse(exactText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
			throw Invalid("Enter an exact catalogue price or leave it Unknown.");
		ValidateKnown(parsed);
		if (legacyValue.HasValue && legacyValue.Value != parsed)
			throw Invalid("The numeric catalogue price and exact price text disagree.");
		return parsed;
	}

	public static string? Format(decimal? value)
	{
		if (!value.HasValue) return null;
		return value.Value.ToString("0.00", CultureInfo.InvariantCulture);
	}

	private static void ValidateKnown(decimal value)
	{
		if (value < 0 || value > Maximum || value % 0.01m != 0)
			throw Invalid("Catalogue price must be nonnegative, within 18 digits and recorded to at most two decimal places.");
	}

	private static BusinessException Invalid(string message) =>
		new BusinessException(LibraryErrorCodes.CatalogueMetadataInvalid, message).WithData("Field", "price");
}
