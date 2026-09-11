using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionSeatRouteRules
{
	public static void EnsureRoute(
		string status,
		AdmissionWorkflowTransitionDefinition? transition,
		IReadOnlyDictionary<string, string> facts)
	{
		if (status == "confirmed") return;
		if (status is not ("waitlisted" or "released" or "expired") ||
			transition == null || transition.IsDefault || string.IsNullOrWhiteSpace(transition.ConditionRuleJson))
			throw Invalid();

		var evaluator = new AdmissionWorkflowConditionEvaluator();
		var withoutStatus = new Dictionary<string, string>(facts, StringComparer.OrdinalIgnoreCase)
		{
			["seat.status"] = "__unavailable__"
		};
		if (!evaluator.Evaluate(transition.ConditionRuleJson, facts) ||
			evaluator.Evaluate(transition.ConditionRuleJson, withoutStatus))
			throw Invalid();

		using var json = JsonDocument.Parse(transition.ConditionRuleJson);
		if (!HasLiteral(json.RootElement, status)) throw Invalid();
	}

	private static bool HasLiteral(JsonElement node, string status)
	{
		if (node.TryGetProperty("conditions", out var children))
			return children.EnumerateArray().Any(child => HasLiteral(child, status));
		if (!node.TryGetProperty("field", out var field) ||
			!string.Equals(field.GetString(), "seat.status", StringComparison.OrdinalIgnoreCase) ||
			!node.TryGetProperty("value", out var value)) return false;
		return node.GetProperty("operator").GetString()?.ToLowerInvariant() switch
		{
			"equals" => value.ToString().Equals(status, StringComparison.OrdinalIgnoreCase),
			"in" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray()
				.Any(item => item.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)),
			_ => false
		};
	}

	private static BusinessException Invalid() => new(AdmissionSeatStageErrorCodes.RouteRequired);
}
