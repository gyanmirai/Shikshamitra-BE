using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

internal sealed record RoomColumnFilter(string Column, string Type, string? Text, double Number = 0, double Maximum = 0);

internal static class RoomColumnFilters
{
	private static readonly HashSet<string> TextColumns = ["roomName", "buildingName", "floorName", "roomTypeName", "features"];
	private static readonly HashSet<string> NumberColumns = ["capacity", "totalBenches"];
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	internal static List<RoomColumnFilter> Parse(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		if (json.Length > 16384) throw Invalid();
		try
		{
			var filters = JsonSerializer.Deserialize<List<FilterInput>>(json, Json) ?? throw Invalid();
			if (filters.Count > 10) throw Invalid();
			return filters.Select(Parse).ToList();
		}
		catch (JsonException) { throw Invalid(); }
	}

	private static RoomColumnFilter Parse(FilterInput input)
	{
		if (input == null) throw Invalid();
		if (TextColumns.Contains(input.Column) && input.Type is "contains" or "equals" or "startsWith" or "endsWith"
			&& input.Value.ValueKind == JsonValueKind.String && input.Value.GetString() is { Length: <= 200 } text)
			return new(input.Column, input.Type, text);
		if (!NumberColumns.Contains(input.Column)) throw Invalid();
		if (input.Type == "between" && input.Value.ValueKind == JsonValueKind.Object
			&& input.Value.TryGetProperty("min", out var min) && input.Value.TryGetProperty("max", out var max))
		{
			var lower = Number(min);
			var upper = Number(max);
			if (lower > upper) throw Invalid();
			return new(input.Column, input.Type, null, lower, upper);
		}
		if (input.Type is "equals" or "greaterThan" or "lessThan") return new(input.Column, input.Type, null, Number(input.Value));
		throw Invalid();
	}

	private static double Number(JsonElement value)
	{
		var valid = value.ValueKind == JsonValueKind.Number ? value.TryGetDouble(out var parsed)
			: double.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : null,
				NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
		return valid && double.IsFinite(parsed) ? parsed : throw Invalid();
	}

	internal static AbpValidationException Invalid() => new("Use up to 10 supported room column filters with valid values.");

	private sealed class FilterInput
	{
		public FilterInput() { }
		public string Column { get; set; } = string.Empty;
		public string Type { get; set; } = string.Empty;
		public JsonElement Value { get; set; }
	}
}
