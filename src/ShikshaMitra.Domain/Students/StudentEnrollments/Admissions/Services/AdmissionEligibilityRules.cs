using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionEligibilityCriterionResult(string Code, string Name, string Kind, string Outcome,
	string Expected, string Actual, string Source, string Message);

public static class AdmissionEligibilityRules
{
	public static string AgeOutcome(DateOnly? birthDate, DateOnly cutoff, int? minimumMonths, int? maximumMonths)
	{
		var months = CompletedAgeMonths(birthDate, cutoff);
		if (!months.HasValue) return "unknown";
		return minimumMonths.HasValue && months < minimumMonths || maximumMonths.HasValue && months > maximumMonths ? "failed" : "passed";
	}
	public static int? CompletedAgeMonths(DateOnly? birthDate, DateOnly cutoff)
	{
		if (!birthDate.HasValue || birthDate > cutoff) return null;
		var months = (cutoff.Year - birthDate.Value.Year) * 12 + cutoff.Month - birthDate.Value.Month;
		return birthDate.Value.AddMonths(months) > cutoff ? months - 1 : months;
	}
	public static string ConditionOutcome(string json, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, AdmissionFormFieldType>? types = null)
	{
		using var document = JsonDocument.Parse(json);
		return Evaluate(document.RootElement, values, types);
	}
	private static string Evaluate(JsonElement node, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, AdmissionFormFieldType>? types)
	{
		var operation = node.GetProperty("operator").GetString()!;
		if (node.TryGetProperty("conditions", out var children))
		{
			var results = children.EnumerateArray().Select(child => Evaluate(child, values, types)).ToList();
			if (operation.Equals("ANY", StringComparison.OrdinalIgnoreCase)) return results.Contains("passed") ? "passed" : results.Contains("unknown") ? "unknown" : "failed";
			return results.Contains("unknown") ? "unknown" : results.Contains("failed") ? "failed" : "passed";
		}
		if (!values.TryGetValue(node.GetProperty("field").GetString()!, out var current)) return "unknown";
		if (string.IsNullOrWhiteSpace(current) && !operation.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase) && !operation.Equals("IsNotEmpty", StringComparison.OrdinalIgnoreCase)) return "unknown";
		if (types != null && types.TryGetValue(node.GetProperty("field").GetString()!, out var type) && type is AdmissionFormFieldType.Number or AdmissionFormFieldType.Checkbox or AdmissionFormFieldType.MultiSelect)
			return AdmissionEligibilityTypedCondition.Evaluate(node, current, type);
		if ((operation.StartsWith("GreaterThan", StringComparison.OrdinalIgnoreCase) || operation.StartsWith("LessThan", StringComparison.OrdinalIgnoreCase)) &&
			!decimal.TryParse(current, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) return "unknown";
		return new AdmissionWorkflowConditionEvaluator().Evaluate(node.GetRawText(), values) ? "passed" : "failed";
	}
	public static string Outcome(IEnumerable<AdmissionEligibilityCriterionResult> rows) =>
		rows.Any(row => row.Outcome == "unknown") ? "needsInformation" : rows.Any(row => row.Outcome == "failed") ? "ineligible" : "eligible";
}
