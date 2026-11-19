using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

public static class AcademicNameFormatter
{
	public static string Format(string value, AcademicTextCase mode)
	{
		ArgumentNullException.ThrowIfNull(value);
		if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
		if (mode == AcademicTextCase.Default) return value;
		if (mode == AcademicTextCase.Uppercase) return value.ToUpperInvariant();
		var words = Regex.Split(value.Trim(), @"\s+").Where(word => word.Length > 0)
			.Select(word => word.ToLowerInvariant()).ToArray();
		return mode switch
		{
			AcademicTextCase.Capitalize => string.Join(" ", words.Select(Capitalize)),
			AcademicTextCase.CamelCase => string.Concat(words.Select((word, index) => index == 0 ? word : Capitalize(word))),
			AcademicTextCase.PascalCase => string.Concat(words.Select(Capitalize)),
			_ => value
		};
	}

	private static string Capitalize(string word)
	{
		var first = StringInfo.GetNextTextElement(word);
		return first.ToUpperInvariant() + word[first.Length..];
	}
}
