using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Reports;

// A view over immutable captured data. Field types always come from server metadata.
public sealed class HrReportingTableQuery
{
	private readonly HrReportTableQueryDto _query;
	private readonly Dictionary<string, HrReportFieldDto> _fields;
	private readonly TimeZoneInfo _timezone;
	private readonly List<List<Func<Dictionary<string, object?>, bool>>> _filters;
	public HrReportingTableQuery(HrReportTableQueryDto? query, List<HrReportFieldDto> fields, string timezone)
	{
		_query = query ?? new();
		_fields = fields.ToDictionary(x => x.Id);
		_timezone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
		if (_query.Search == null || _query.Search.Length > 256 || JsonSerializer.Serialize(_query, HrReportingEngine.Json).Length > 32768 || _query.Filters == null || _query.Sorts == null || _query.Filters.Count > 30 || _query.Sorts.Count > 60)
			throw HrReportingEngine.Invalid("Use up to 30 filters, 60 sort columns, and a search of at most 256 characters.");
		if (_query.Sorts.Any(x => x == null || string.IsNullOrWhiteSpace(x.Column) || !_fields.ContainsKey(x.Column) || x.Direction is not ("asc" or "desc")) || _query.Sorts.Select(x => x.Column).Distinct().Count() != _query.Sorts.Count)
			throw HrReportingEngine.Invalid("Choose distinct columns present in this report and ascending or descending order.");
		if (_query.Filters.Any(x => x == null || !_fields.ContainsKey(x.Column))) throw HrReportingEngine.Invalid("Choose filter columns present in this report.");
		// Shared table semantics: alternatives in one column, intersection across columns.
		_filters = _query.Filters.GroupBy(x => x.Column).Select(group => group.Select(Compile).ToList()).ToList();
	}
	public IEnumerable<T> Apply<T>(IEnumerable<T> rows, Func<T, Dictionary<string, object?>> values)
	{
		var matching = rows.Where(row => Matches(values(row)));
		IOrderedEnumerable<T>? sorted = null;
		foreach (var sort in _query.Sorts)
		{
			Func<T, object?> key = row => SortValue(values(row).GetValueOrDefault(sort.Column), _fields[sort.Column].Type);
			sorted = sorted == null
				? sort.Direction == "asc" ? matching.OrderBy(key, ValueComparer.Instance) : matching.OrderByDescending(key, ValueComparer.Instance)
				: sort.Direction == "asc" ? sorted.ThenBy(key, ValueComparer.Instance) : sorted.ThenByDescending(key, ValueComparer.Instance);
		}
		// LINQ ordering is stable, preserving capture order (or the history ID tie-breaker).
		return sorted ?? matching;
	}
	private bool Matches(Dictionary<string, object?> row) =>
		(string.IsNullOrWhiteSpace(_query.Search) || _fields.Values.Any(field =>
			Text(row.GetValueOrDefault(field.Id), field.Type).Contains(_query.Search.Trim(), StringComparison.OrdinalIgnoreCase) ||
			(Convert.ToString(row.GetValueOrDefault(field.Id), CultureInfo.InvariantCulture) ?? "").Contains(_query.Search.Trim(), StringComparison.OrdinalIgnoreCase)))
		&& _filters.All(group => group.Any(match => match(row)));
	private Func<Dictionary<string, object?>, bool> Compile(HrReportTableFilterDto filter)
	{
		var type = _fields[filter.Column].Type;
		var allowed = type switch {
			"number" or "money" => new[] { "equals", "greaterThan", "lessThan", "between" },
			"date" or "datetime" => new[] { "equals", "after", "before" },
			"boolean" => new[] { "equals" },
			_ => new[] { "contains", "equals", "startsWith", "endsWith" } };
		if (!allowed.Contains(filter.Type)) throw HrReportingEngine.Invalid("Choose an operator supported by this report column.");
		object? expected;
		object? upper = null;
		if (filter.Type == "between") {
			if (filter.Value.ValueKind != JsonValueKind.Object || !filter.Value.TryGetProperty("min", out var min) || !filter.Value.TryGetProperty("max", out var max)) throw HrReportingEngine.Invalid("Provide both range limits.");
			expected = Parse(min, type); upper = Parse(max, type);
			if (ValueComparer.Instance.Compare(expected, upper) > 0) throw HrReportingEngine.Invalid("The minimum must not exceed the maximum.");
		} else expected = Parse(filter.Value, type);
		if (expected is DateTime expectedTime) expected = new DateTime(expectedTime.Year, expectedTime.Month, expectedTime.Day, expectedTime.Hour, expectedTime.Minute, 0);
		return row => {
			var raw = row.GetValueOrDefault(filter.Column);
			if (raw == null || raw is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }) return false;
			var actual = Typed(raw, type);
			if (actual == null) return false;
			if (actual is DateTime timestamp) actual = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0);
			var comparison = ValueComparer.Instance.Compare(actual, expected);
			return filter.Type switch {
				"contains" => ((string)actual).Contains((string)expected!, StringComparison.OrdinalIgnoreCase),
				"startsWith" => ((string)actual).StartsWith((string)expected!, StringComparison.OrdinalIgnoreCase),
				"endsWith" => ((string)actual).EndsWith((string)expected!, StringComparison.OrdinalIgnoreCase),
				"equals" => comparison == 0,
				"greaterThan" => comparison > 0, "lessThan" => comparison < 0,
				"after" => comparison >= 0, "before" => comparison <= 0,
				"between" => comparison >= 0 && ValueComparer.Instance.Compare(actual, upper) <= 0,
				_ => false };
		};
	}
	private object Parse(JsonElement value, string type)
	{
		if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) || value.ToString().Length is < 1 or > 2048)
			throw HrReportingEngine.Invalid("Enter a valid filter value.");
		return Typed(value, type) ?? throw HrReportingEngine.Invalid("Enter a valid number, date, or boolean for this column.");
	}
	private object? Typed(object? value, string type)
	{
		if (value == null || value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }) return null;
		var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
		if (type is "number" or "money") return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
		if (type == "boolean") return bool.TryParse(text, out var boolean) ? boolean : null;
		if (type == "enum") return new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
		if (type == "clock" && (text.Contains(':') || text.Contains("AM", StringComparison.OrdinalIgnoreCase) || text.Contains("PM", StringComparison.OrdinalIgnoreCase)) &&
			TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		if (type == "date") return DateOnly.TryParseExact(text.Length >= 10 ? text[..10] : text, new[] { "yyyy-MM-dd", "dd-MM-yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
		if (type == "datetime") {
			DateTime local;
			if (DateTime.TryParseExact(text, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local)) return local;
			if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)) return null;
			local = TimeZoneInfo.ConvertTime(timestamp, _timezone).DateTime;
			return local;
		}
		return text;
	}
	private object? SortValue(object? value, string type)
	{
		var text = Convert.ToString(value, CultureInfo.InvariantCulture);
		// Unlimited is ordered above finite amounts, while remaining excluded from finite filters.
		if (type is "money" or "number" && text is "No ceiling" or "No upper limit" or "Unlimited") return decimal.MaxValue;
		return type == "datetime" && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
			? timestamp.UtcDateTime : Typed(value, type);
	}
	private string Text(object? value, string type)
	{
		var typed = Typed(value, type);
		return typed switch { bool boolean => boolean ? "Yes" : "No", DateOnly date => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), DateTime date => date.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture), _ => Convert.ToString(typed, CultureInfo.InvariantCulture) ?? "" };
	}
	private sealed class ValueComparer : IComparer<object?>
	{
		public static readonly ValueComparer Instance = new();
		public int Compare(object? x, object? y) => x == null ? y == null ? 0 : -1 : y == null ? 1 : x is string a && y is string b ? StringComparer.OrdinalIgnoreCase.Compare(a, b) : ((IComparable)x).CompareTo(y);
	}
}
