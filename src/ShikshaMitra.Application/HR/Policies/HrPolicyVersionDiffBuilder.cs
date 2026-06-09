using System;
using System.Collections.Generic;
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
		var fromProps = fromDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
		var toProps = toDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
		return fromProps.Keys.Union(toProps.Keys)
			.Where(field => !JsonValuesEqual(fromProps.GetValueOrDefault(field), toProps.GetValueOrDefault(field)))
			.Select(field => new HrPolicyVersionDifferenceDto
			{
				Field = field,
				FieldKey = field,
				FieldLabel = FieldLabel(field),
				Category = FieldCategory(field),
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

	private static bool JsonValuesEqual(JsonElement left, JsonElement right) =>
		string.Equals(GetRawValue(left), GetRawValue(right), StringComparison.Ordinal);

	private static string? GetRawValue(JsonElement value) =>
		value.ValueKind == JsonValueKind.Undefined ? null : value.GetRawText();
}
