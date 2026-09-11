using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionEligibilityConditionDescription
{
	public static string Describe(string json, IReadOnlyDictionary<string, string> labels)
	{
		using var document = JsonDocument.Parse(json);
		return DescribeNode(document.RootElement, labels);
	}
	private static string DescribeNode(JsonElement node, IReadOnlyDictionary<string, string> labels)
	{
		var op = node.GetProperty("operator").GetString()!.ToLowerInvariant();
		if (node.TryGetProperty("conditions", out var children)) return "(" + string.Join(op == "any" ? " or " : " and ", children.EnumerateArray().Select(child => DescribeNode(child, labels))) + ")";
		var field = node.GetProperty("field").GetString()!;
		var label = labels.GetValueOrDefault(field, field);
		var phrase = op switch { "equals" => "equals", "notequals" => "does not equal", "contains" => "contains", "notcontains" => "does not contain",
			"startswith" => "starts with", "endswith" => "ends with", "greaterthan" => "is above", "greaterthanorequal" => "is at least",
			"lessthan" => "is below", "lessthanorequal" => "is at most", "in" => "is one of", "notin" => "is not one of", "isempty" => "is blank", "isnotempty" => "is provided", _ => op };
		if (op is "isempty" or "isnotempty") return $"{label} {phrase}";
		var value = node.GetProperty("value");
		return $"{label} {phrase} {(value.ValueKind == JsonValueKind.Array ? string.Join(", ", value.EnumerateArray().Select(item => item.ToString())) : value.ToString())}";
	}
}
