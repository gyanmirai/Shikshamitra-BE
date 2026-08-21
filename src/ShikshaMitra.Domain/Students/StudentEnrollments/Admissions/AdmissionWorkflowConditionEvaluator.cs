using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowConditionEvaluator : DomainService
{
	public bool Evaluate(string? conditionRuleJson, IReadOnlyDictionary<string, string> values)
	{
		if (string.IsNullOrWhiteSpace(conditionRuleJson)) return true;
		using var document = JsonDocument.Parse(conditionRuleJson);
		return EvaluateNode(document.RootElement, values);
	}

	private static bool EvaluateNode(JsonElement node, IReadOnlyDictionary<string, string> values)
	{
		var operation = node.GetProperty("operator").GetString() ?? string.Empty;
		if (node.TryGetProperty("conditions", out var conditions))
		{
			var results = conditions.EnumerateArray().Select(condition => EvaluateNode(condition, values));
			return operation.Equals("ANY", StringComparison.OrdinalIgnoreCase)
				? results.Any(result => result)
				: results.All(result => result);
		}

		var field = node.GetProperty("field").GetString() ?? string.Empty;
		var current = values.GetValueOrDefault(field, string.Empty);
		var expected = node.TryGetProperty("value", out var expectedNode) ? ReadValue(expectedNode) : string.Empty;
		return operation.ToLowerInvariant() switch
		{
			"equals" => string.Equals(current, expected, StringComparison.OrdinalIgnoreCase),
			"notequals" => !string.Equals(current, expected, StringComparison.OrdinalIgnoreCase),
			"contains" => current.Contains(expected, StringComparison.OrdinalIgnoreCase),
			"notcontains" => !current.Contains(expected, StringComparison.OrdinalIgnoreCase),
			"startswith" => current.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
			"endswith" => current.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
			"isempty" => string.IsNullOrWhiteSpace(current),
			"isnotempty" => !string.IsNullOrWhiteSpace(current),
			"greaterthan" => CompareNumbers(current, expected, (left, right) => left > right),
			"greaterthanorequal" => CompareNumbers(current, expected, (left, right) => left >= right),
			"lessthan" => CompareNumbers(current, expected, (left, right) => left < right),
			"lessthanorequal" => CompareNumbers(current, expected, (left, right) => left <= right),
			"in" => ReadCollection(node).Contains(current, StringComparer.OrdinalIgnoreCase),
			"notin" => !ReadCollection(node).Contains(current, StringComparer.OrdinalIgnoreCase),
			_ => false
		};
	}

	private static string ReadValue(JsonElement value) =>
		value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

	private static IEnumerable<string> ReadCollection(JsonElement node)
	{
		var value = node.GetProperty("value");
		return value.ValueKind == JsonValueKind.Array
			? value.EnumerateArray().Select(ReadValue)
			: ReadValue(value).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	}

	private static bool CompareNumbers(string left, string right, Func<decimal, decimal, bool> compare) =>
		decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber) &&
		decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber) &&
		compare(leftNumber, rightNumber);
}
