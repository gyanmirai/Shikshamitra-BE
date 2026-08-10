using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicBranding
{
	internal static IReadOnlyList<string> ParseSupportedLanguages(string? configured)
	{
		var languages = string.IsNullOrWhiteSpace(configured)
			? new List<string> { "en", "gu", "hi" }
			: configured
				.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(value => value.ToLowerInvariant())
				.Where(IsSafeLanguageTag)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Take(10)
				.ToList();
		if (!languages.Contains("en", StringComparer.OrdinalIgnoreCase))
		{
			languages.Insert(0, "en");
		}

		return languages.Count > 0 ? languages : ["en", "gu", "hi"];
	}

	internal static string NormalizeLanguage(string? configured, IReadOnlyList<string> supportedLanguages)
	{
		var language = configured?.Trim().ToLowerInvariant();
		if (language != null && supportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
		{
			return supportedLanguages.First(value => string.Equals(value, language, StringComparison.OrdinalIgnoreCase));
		}

		var baseLanguage = language?.Split('-', 2)[0];
		return baseLanguage != null && supportedLanguages.Contains(baseLanguage, StringComparer.OrdinalIgnoreCase)
			? supportedLanguages.First(value => string.Equals(value, baseLanguage, StringComparison.OrdinalIgnoreCase))
			: supportedLanguages[0];
	}

	internal static string? NormalizeColor(string? value)
	{
		var color = value?.Trim();
		return color?.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit)
			? color.ToUpperInvariant()
			: null;
	}

	internal static string? BuildLogoDataUrl(byte[]? logo, string? contentType)
	{
		if (logo == null || logo.Length == 0 || logo.Length > 512 * 1024)
		{
			return null;
		}

		var safeContentType = contentType?.Trim().ToLowerInvariant() switch
		{
			"image/jpeg" => "image/jpeg",
			"image/webp" => "image/webp",
			_ => "image/png"
		};
		return $"data:{safeContentType};base64,{Convert.ToBase64String(logo)}";
	}

	internal static string? FirstValue(params string?[] values) =>
		values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

	private static bool IsSafeLanguageTag(string value) =>
		value.Length is > 0 and <= 10 && value.All(character => char.IsLetter(character) || character == '-');
}
