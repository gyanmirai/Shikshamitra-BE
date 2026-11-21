using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Finance.ExternalReceivables;

public static partial class FinanceExternalEvidenceJson
{
	public const string WhiteSpaceCharacters = "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680" +
		"\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";

	public static string? Scalar(string? document, string? path, string? expectedKind)
	{
		if (document == null || string.IsNullOrEmpty(path) || expectedKind == null) return null;
		var keys = path.Split('.');
		if (keys.Length > 8) return null;
		try
		{
			using var json = JsonDocument.Parse(document);
			var value = json.RootElement;
			foreach (var key in keys)
			{
				if (value.ValueKind != JsonValueKind.Object) return null;
				var count = 0;
				var next = default(JsonElement);
				foreach (var property in value.EnumerateObject())
				{
					if (property.Name.Contains('\0')) return null;
					if (!property.NameEquals(key)) continue;
					count++;
					next = property.Value;
				}
				if (count != 1) return null;
				value = next;
			}
			return (expectedKind, value.ValueKind) switch
			{
				("null", JsonValueKind.Null) => "null",
				("string", JsonValueKind.String) => ReadString(value),
				("number", JsonValueKind.Number) => value.GetRawText(),
				("boolean", JsonValueKind.True or JsonValueKind.False) => value.GetRawText(),
				_ => null
			};
		}
		catch (JsonException) { return null; }
		// GetString rejects incomplete escaped UTF-16 even when its JSON token parsed.
		catch (InvalidOperationException) { return null; }
	}

	public static decimal? Money(string? document, string? path)
	{
		var raw = Scalar(document, path, "number");
		if (raw == null || raw.Length > 45 || !MoneyToken().IsMatch(raw)) return null;
		var normalized = raw.Contains('.') ? raw.TrimEnd('0').TrimEnd('.') : raw;
		var point = normalized.IndexOf('.');
		if (point >= 0 && normalized.Length - point - 1 > 2) return null;
		return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
			&& amount <= 9999999999999999.99m ? amount : null;
	}

	private static string? ReadString(JsonElement value)
	{
		var text = value.GetString();
		return text?.Contains('\0') == true ? null : text;
	}

	[GeneratedRegex(@"^(0|[1-9][0-9]{0,15})(\.[0-9]{1,28})?$")]
	private static partial Regex MoneyToken();
}
