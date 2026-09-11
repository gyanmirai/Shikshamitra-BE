using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionEligibilityEvidence
{
	public static Dictionary<string, string> Facts(AdmissionEligibilityEvaluation evaluation, string status, bool stale)
	{
		var rows = JsonSerializer.Deserialize<List<AdmissionEligibilityCriterionResult>>(evaluation.CriteriaSnapshotJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
		return new(StringComparer.OrdinalIgnoreCase)
		{
			["eligibility.status"] = stale ? "stale" : status,
			["eligibility.failedCount"] = rows.Count(row => row.Outcome == "failed").ToString(),
			["eligibility.unknownCount"] = rows.Count(row => row.Outcome == "unknown").ToString()
		};
	}
	public static void EnsureRoute(string status, AdmissionWorkflowTransitionDefinition? transition, IReadOnlyDictionary<string, string> facts)
	{
		if (status is "eligible" or "exceptionApproved") return;
		if (transition == null || transition.IsDefault || string.IsNullOrWhiteSpace(transition.ConditionRuleJson)) throw Invalid();
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		var withoutStatus = new Dictionary<string, string>(facts, StringComparer.OrdinalIgnoreCase) { ["eligibility.status"] = "__unavailable__" };
		if (!evaluator.Evaluate(transition.ConditionRuleJson, facts) || evaluator.Evaluate(transition.ConditionRuleJson, withoutStatus)) throw Invalid();
		using var json = JsonDocument.Parse(transition.ConditionRuleJson);
		if (!HasLiteral(json.RootElement, status)) throw Invalid();
	}
	private static bool HasLiteral(JsonElement node, string status)
	{
		if (node.TryGetProperty("conditions", out var children)) return children.EnumerateArray().Any(child => HasLiteral(child, status));
		if (!node.TryGetProperty("field", out var field) || !string.Equals(field.GetString(), "eligibility.status", StringComparison.OrdinalIgnoreCase) || !node.TryGetProperty("value", out var value)) return false;
		var op = node.GetProperty("operator").GetString();
		return op?.ToLowerInvariant() switch
		{
			"equals" => value.ToString().Equals(status, StringComparison.OrdinalIgnoreCase),
			"in" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(item => item.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)),
			_ => false
		};
	}
	private static BusinessException Invalid() => new(AdmissionEligibilityErrorCodes.RouteRequired);
}
