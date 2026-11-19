using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace ShikshaMitra.Students.Records;

public static class HouseManagementInputRules
{
	public static bool IsColor(string? value) => value != null && value.Length == 7 && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);

	public static bool IsLogoUrl(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return true;
		if (value.Length > 500 || value.Contains('\\') || value.Any(char.IsControl)) return false;
		if (value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal)) return true;
		return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
			(uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(uri.UserInfo);
	}
}
