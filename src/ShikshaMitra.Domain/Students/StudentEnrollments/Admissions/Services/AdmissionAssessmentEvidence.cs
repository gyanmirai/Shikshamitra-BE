using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionAssessmentEvidence
{
	public static Dictionary<string, string> GetFacts(AdmissionAssessmentAttempt? attempt)
	{
		if (attempt == null) return [];
		var values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
		{
			["assessment.outcome"] = attempt.Status == "completed" ? attempt.Outcome : "pending",
			["assessment.attemptNumber"] = attempt.AttemptNumber.ToString(CultureInfo.InvariantCulture),
			["assessment.attendance"] = attempt.Attendance,
			["assessment.reviewed"] = attempt.ReviewedBy.HasValue && attempt.Status == "completed" ? "true" : "false"
		};
		if (attempt.Marks.HasValue && attempt.Status == "completed")
			values["assessment.marks"] = attempt.Marks.Value.ToString(CultureInfo.InvariantCulture);
		return values;
	}

	public static List<string> GetBlockers(AdmissionAssessmentAttempt? attempt)
	{
		if (attempt == null) return ["Create an assessment attempt or record an authorized waiver."];
		if (attempt.Status == "pendingReview") return ["An authorized reviewer must approve the assessment result."];
		if (attempt.Status != "completed") return ["Record the required assessment attendance and result."];
		return [];
	}

	public static bool RequiresOutcomeRoute(AdmissionAssessmentAttempt? attempt) =>
		attempt?.Outcome is "failed" or "absent" or "retake";

	public static bool HasExplicitOutcomeRoute(AdmissionWorkflowTransitionDefinition? transition,
		AdmissionAssessmentAttempt attempt, IReadOnlyDictionary<string, string> facts)
	{
		if (transition == null || transition.IsDefault || string.IsNullOrWhiteSpace(transition.ConditionRuleJson)) return false;
		using var document = JsonDocument.Parse(transition.ConditionRuleJson);
		return HasMatchingOutcome(document.RootElement, attempt.Outcome) &&
			new AdmissionWorkflowConditionEvaluator().Evaluate(transition.ConditionRuleJson, facts);
	}

	public static void EnsureComplete(AdmissionAssessmentAttempt? attempt)
	{
		var blockers = GetBlockers(attempt);
		if (blockers.Count > 0) throw new BusinessException(AdmissionAssessmentErrorCodes.EvidenceRequired, blockers[0]);
	}

	public static void EnsureRoute(AdmissionAssessmentAttempt? attempt,
		AdmissionWorkflowTransitionDefinition? transition, IReadOnlyDictionary<string, string> facts)
	{
		if (RequiresOutcomeRoute(attempt) && !HasExplicitOutcomeRoute(transition, attempt!, facts))
			throw new BusinessException(AdmissionAssessmentErrorCodes.RouteRequired,
				"Add and select a route for this assessment result, or arrange an allowed retake.");
	}

	private static bool HasMatchingOutcome(JsonElement node, string outcome)
	{
		if (node.TryGetProperty("conditions", out var children)) return children.EnumerateArray().Any(child => HasMatchingOutcome(child, outcome));
		if (!node.TryGetProperty("field", out var field) || !string.Equals(field.GetString(), "assessment.outcome", System.StringComparison.OrdinalIgnoreCase) ||
			!node.TryGetProperty("value", out var value)) return false;
		var operation = node.GetProperty("operator").GetString();
		return operation?.ToLowerInvariant() switch
		{
			"equals" => string.Equals(value.ToString(), outcome, System.StringComparison.OrdinalIgnoreCase),
			"in" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(item => item.ToString() == outcome),
			_ => false
		};
	}
}
