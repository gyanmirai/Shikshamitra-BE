using System;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Settings;

public static class FeeSetupNameNormalizer
{
	private const string AllowedPunctuation = "-&/'’.(),+:%#";

	public static string ForStorage(string value) =>
		string.Join(' ', (value ?? string.Empty)
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	public static string ForComparison(string value) => ForStorage(value).ToUpperInvariant();

	public static bool HasSensibleCharacters(string value) =>
		!string.IsNullOrWhiteSpace(value) && value.All(character =>
			IsEnglishLetterOrNumber(character) ||
			char.IsWhiteSpace(character) ||
			AllowedPunctuation.Contains(character));

	private static bool IsEnglishLetterOrNumber(char character) =>
		character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
