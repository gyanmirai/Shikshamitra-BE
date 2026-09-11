using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionEligibilityTypedCondition
{
	public static string Evaluate(JsonElement node, string current, AdmissionFormFieldType type)
	{
		var op = node.GetProperty("operator").GetString()!.ToLowerInvariant();
		var empty = string.IsNullOrWhiteSpace(current) || type == AdmissionFormFieldType.MultiSelect && current.Trim() == "[]";
		if (op is "isempty" or "isnotempty") return Result(op == "isempty" ? empty : !empty);
		if (empty) return "unknown";
		var expected = node.GetProperty("value");
		var list = expected.ValueKind == JsonValueKind.Array ? expected.EnumerateArray().Select(item => item.ToString()).ToList() :
			 op is "in" or "notin" ? expected.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : [expected.ToString()];
		if (type == AdmissionFormFieldType.Number)
		{
			if (!decimal.TryParse(current, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || list.Any(item => !decimal.TryParse(item, NumberStyles.Number, CultureInfo.InvariantCulture, out _))) return "unknown";
			var numbers = list.Select(item => decimal.Parse(item, NumberStyles.Number, CultureInfo.InvariantCulture)).ToList();
			if (numbers.Count == 0) return "unknown";
			return Result(op switch { "equals" => number == numbers[0], "notequals" => number != numbers[0], "in" => numbers.Contains(number), "notin" => !numbers.Contains(number),
				"greaterthan" => number > numbers[0], "greaterthanorequal" => number >= numbers[0], "lessthan" => number < numbers[0], "lessthanorequal" => number <= numbers[0], _ => false });
		}
		if (type == AdmissionFormFieldType.Checkbox)
		{
			if (!bool.TryParse(current, out var actual) || !bool.TryParse(expected.ToString(), out var target)) return "unknown";
			return Result(op == "equals" ? actual == target : op == "notequals" && actual != target);
		}
		try
		{
			using var json = JsonDocument.Parse(current);
			if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String)) return "unknown";
			var selected = json.RootElement.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
			return Result(op switch { "contains" => selected.Contains(expected.ToString()), "notcontains" => !selected.Contains(expected.ToString()),
				"in" => selected.Overlaps(list), "notin" => !selected.Overlaps(list), "equals" => selected.SetEquals(list), "notequals" => !selected.SetEquals(list), _ => false });
		}
		catch (JsonException) { return "unknown"; }
	}
	private static string Result(bool passed) => passed ? "passed" : "failed";
}
