using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionReviewRouteRules
{
	public static void EnsureRoute(string decision, AdmissionWorkflowTransitionDefinition? transition, IReadOnlyDictionary<string, string> facts)
	{
		if (decision == "approve") return;
		if (decision is not ("waitlist" or "reject") || transition == null || transition.IsDefault || string.IsNullOrWhiteSpace(transition.ConditionRuleJson)) throw Invalid();
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		var without = new Dictionary<string, string>(facts, StringComparer.OrdinalIgnoreCase) { ["review.decision"] = "__unavailable__" };
		if (!evaluator.Evaluate(transition.ConditionRuleJson, facts) || evaluator.Evaluate(transition.ConditionRuleJson, without)) throw Invalid();
		using var json = JsonDocument.Parse(transition.ConditionRuleJson);
		if (!HasLiteral(json.RootElement, decision)) throw Invalid();
	}
	private static bool HasLiteral(JsonElement node, string decision)
	{
		if (node.TryGetProperty("conditions", out var children)) return children.EnumerateArray().Any(child => HasLiteral(child, decision));
		if (!node.TryGetProperty("field", out var field) || !string.Equals(field.GetString(), "review.decision", StringComparison.OrdinalIgnoreCase) || !node.TryGetProperty("value", out var value)) return false;
		return node.GetProperty("operator").GetString()?.ToLowerInvariant() switch
		{
			"equals" => value.ToString().Equals(decision, StringComparison.OrdinalIgnoreCase),
			"in" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(item => item.ToString().Equals(decision, StringComparison.OrdinalIgnoreCase)),
			_ => false
		};
	}
	private static BusinessException Invalid() => new(AdmissionReviewErrorCodes.RouteRequired);
}
