using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using static ShikshaMitra.HR.Policies.HrPolicyVersionDisplayFormatter;
using static ShikshaMitra.HR.Policies.HrPolicyVersionFieldMetadata;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyVersionDiffBuilder
{
	public static List<HrPolicyVersionDifferenceDto> BuildDifferences(
		string fromJson,
		string toJson,
		HrPolicyVersionDisplayContext displayContext)
	{
		using var fromDoc = ParseSnapshot(fromJson);
		using var toDoc = ParseSnapshot(toJson);
		var fromProps = FlattenSnapshot(fromDoc.RootElement);
		var toProps = FlattenSnapshot(toDoc.RootElement);
		return fromProps.Keys.Union(toProps.Keys)
			.Where(field => !JsonValuesEqual(
				field,
				fromProps.GetValueOrDefault(field),
				toProps.GetValueOrDefault(field)))
			.Select(field => new HrPolicyVersionDifferenceDto
			{
				Field = field,
				FieldKey = field,
				FieldLabel = field.StartsWith("rules.", StringComparison.Ordinal) ? $"{field.Split('.')[1]}: {FieldLabel(field.Split('.')[2])}" : FieldLabel(field),
				Category = field.StartsWith("rules.", StringComparison.Ordinal) ? "Leave rules" : FieldCategory(field),
				FromRawValue = GetRawValue(fromProps.GetValueOrDefault(field)),
				ToRawValue = GetRawValue(toProps.GetValueOrDefault(field)),
				FromDisplayValue = FormatDisplayValue(field, fromProps.GetValueOrDefault(field), displayContext),
				ToDisplayValue = FormatDisplayValue(field, toProps.GetValueOrDefault(field), displayContext),
				FromValue = FormatDisplayValue(field, fromProps.GetValueOrDefault(field), displayContext),
				ToValue = FormatDisplayValue(field, toProps.GetValueOrDefault(field), displayContext)
			})
			.OrderBy(x => x.Category)
			.ThenBy(x => x.FieldLabel)
			.ToList();
	}

	private static Dictionary<string, JsonElement> FlattenSnapshot(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object)
			throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
		var fields = root.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
		// Leave snapshots use the existing Paid/Unpaid bucket as the stable comparison key.
		if (!fields.TryGetValue("rules", out var rules) || rules.ValueKind != JsonValueKind.Array) return fields;
		fields.Remove("rules");
		foreach (var rule in rules.EnumerateArray())
		{
			if (rule.ValueKind != JsonValueKind.Object || !rule.TryGetProperty("paidType", out var paidType) ||
				paidType.ValueKind != JsonValueKind.String || paidType.GetString() is not ("Paid" or "Unpaid"))
				throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
			foreach (var property in rule.EnumerateObject().Where(x => x.Name != "id" && x.Name != "paidType"))
			{
				if (!fields.TryAdd($"rules.{paidType.GetString()}.{property.Name}", property.Value.Clone()))
					throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
			}
		}
		return fields;
	}

	public static void CollectGuids(JsonElement element, HashSet<Guid> ids)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				if (Guid.TryParse(element.GetString(), out var id))
				{
					ids.Add(id);
				}
				break;
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				{
					CollectGuids(property.Value, ids);
				}
				break;
			case JsonValueKind.Array:
				foreach (var item in element.EnumerateArray())
				{
					CollectGuids(item, ids);
				}
				break;
		}
	}

	public static string ToDisplayText(string value)
		=> HrPolicyVersionDisplayFormatter.ToDisplayText(value);

	public static JsonDocument ParseSnapshot(string snapshotJson)
	{
		try
		{
			return JsonDocument.Parse(snapshotJson);
		}
		catch (JsonException ex)
		{
			throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot, innerException: ex);
		}
	}

	private static bool JsonValuesEqual(string field, JsonElement left, JsonElement right)
	{
		if (string.Equals(GetRawValue(left), GetRawValue(right), StringComparison.Ordinal))
		{
			return true;
		}

		if (!field.Equals("effectiveFrom", StringComparison.OrdinalIgnoreCase) &&
			!field.Equals("effectiveTo", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return TryReadDate(left, out var leftDate) &&
			TryReadDate(right, out var rightDate) &&
			leftDate == rightDate;
	}

	private static bool TryReadDate(JsonElement element, out DateOnly date)
	{
		date = default;
		if (element.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		var value = element.GetString()?.Trim();
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		if (value.Length >= 8 &&
			value.Take(8).All(char.IsDigit) &&
			DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
		{
			return true;
		}

		return DateTimeOffset.TryParse(
			value,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal,
			out var timestamp) &&
			(date = DateOnly.FromDateTime(timestamp.Date)) != default;
	}

	private static string? GetRawValue(JsonElement value) =>
		value.ValueKind == JsonValueKind.Undefined ? null : value.GetRawText();
}
