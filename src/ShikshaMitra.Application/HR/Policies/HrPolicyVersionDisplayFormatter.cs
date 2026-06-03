using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyVersionDisplayFormatter
{
	public static string FormatDisplayValue(
		string field,
		JsonElement value,
		HrPolicyVersionDisplayContext displayContext)
	{
		if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
		{
			return "Not set";
		}

		if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
		{
			return value.GetBoolean() ? "Enabled" : "Disabled";
		}

		if (value.ValueKind == JsonValueKind.String)
		{
			var text = value.GetString();
			if (string.IsNullOrWhiteSpace(text))
			{
				return "Not set";
			}
			if (IsDateDisplayField(field) && TryFormatVersionDateTime(text, out var formattedDate))
			{
				return formattedDate;
			}
			if (Guid.TryParse(text, out var id))
			{
				return ResolveGuidDisplay(field, id, displayContext);
			}
			return ToDisplayText(text);
		}

		if (value.ValueKind == JsonValueKind.Number)
		{
			return value.GetRawText();
		}

		if (value.ValueKind == JsonValueKind.Array)
		{
			return FormatArrayValue(field, value, displayContext);
		}

		if (value.ValueKind == JsonValueKind.Object)
		{
			return FormatObjectValue(field, value, displayContext);
		}

		return "Not set";
	}

	public static string ToDisplayText(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
		{
			return "Not set";
		}

		var spaced = value.Replace("_", " ").Replace("-", " ");
		return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
	}

	private static string ResolveGuidDisplay(string field, Guid id, HrPolicyVersionDisplayContext displayContext)
	{
		if (field.EndsWith("creatorId", StringComparison.OrdinalIgnoreCase) ||
			field.EndsWith("modifierId", StringComparison.OrdinalIgnoreCase))
		{
			return displayContext.UserNames.GetValueOrDefault(id) ?? "Unknown user";
		}

		return displayContext.PolicyNames.GetValueOrDefault(id) ?? "Unknown policy";
	}

	private static bool IsDateDisplayField(string field) =>
		field.Equals("creationTime", StringComparison.OrdinalIgnoreCase) ||
		field.Equals("lastModificationTime", StringComparison.OrdinalIgnoreCase) ||
		field.Equals("effectiveFrom", StringComparison.OrdinalIgnoreCase) ||
		field.Equals("effectiveTo", StringComparison.OrdinalIgnoreCase);

	private static bool TryFormatVersionDateTime(string value, out string formatted)
	{
		var normalized = value.Trim();
		if (normalized.Length >= 9 &&
			char.IsDigit(normalized[0]) &&
			char.IsDigit(normalized[1]) &&
			char.IsDigit(normalized[2]) &&
			char.IsDigit(normalized[3]) &&
			char.IsDigit(normalized[4]) &&
			char.IsDigit(normalized[5]) &&
			char.IsDigit(normalized[6]) &&
			char.IsDigit(normalized[7]) &&
			normalized[8] == 'T')
		{
			normalized = $"{normalized[..4]}-{normalized[4..6]}-{normalized[6..8]}{normalized[8..]}";
		}

		if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
		{
			formatted = parsed.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture);
			return true;
		}

		formatted = string.Empty;
		return false;
	}

	private static string FormatArrayValue(string field, JsonElement value, HrPolicyVersionDisplayContext displayContext)
	{
		var items = value.EnumerateArray().ToList();
		if (items.Count == 0)
		{
			return "None";
		}

		return field switch
		{
			"manualMonthDays" => string.Join(", ", items.Select(FormatManualMonth)),
			"holidayOverrides" => $"{items.Count} holiday rule{(items.Count == 1 ? string.Empty : "s")}",
			"holidays" => $"{items.Count} holiday{(items.Count == 1 ? string.Empty : "s")}",
			"customWeeklyOffs" => string.Join(", ", items.Select(x => HrPolicyVersionFieldMetadata.DayName(x.GetInt32()))),
			_ => string.Join(", ", items.Take(3).Select(item => FormatDisplayValue(field, item, displayContext))) +
				(items.Count > 3 ? $" +{items.Count - 3} more" : string.Empty)
		};
	}

	private static string FormatObjectValue(string field, JsonElement value, HrPolicyVersionDisplayContext displayContext)
	{
		if (Guid.TryParse(value.ToString(), out var id))
		{
			return ResolveGuidDisplay(field, id, displayContext);
		}
		return "Changed";
	}

	private static string FormatManualMonth(JsonElement value)
	{
		if (value.ValueKind != JsonValueKind.Object)
		{
			return ToDisplayText(value.ToString());
		}
		var month = value.TryGetProperty("month", out var monthValue) ? monthValue.GetInt32() : 0;
		var year = value.TryGetProperty("year", out var yearValue) ? yearValue.GetInt32() : 0;
		var days = value.TryGetProperty("manualDays", out var daysValue) ? daysValue.GetInt32() : 0;
		return month > 0 && year > 0 ? $"{year}-{month:00}: {days} days" : $"{days} days";
	}
}

internal sealed class HrPolicyVersionDisplayContext
{
	public Dictionary<Guid, string> PolicyNames { get; } = new();
	public Dictionary<Guid, string> UserNames { get; } = new();
}
