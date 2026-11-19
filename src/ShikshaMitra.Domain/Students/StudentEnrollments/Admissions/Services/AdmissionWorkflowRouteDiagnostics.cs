using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionWorkflowRouteDiagnostics
{
	public static IEnumerable<AdmissionWorkflowValidationIssue> Validate(
		IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages,
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> transitions)
	{
		var seen = new HashSet<(Guid From, string Field, string Operator, string Value)>();
		foreach (var route in transitions)
		{
			var source = stages.FirstOrDefault(item => item.Id == route.FromStageId);
			if (route.IsDefault)
			{
				var offerOutcomes = source?.StageType == AdmissionWorkflowStageType.Offer ? AllowedOutcomes(source, "offer.status") : null;
				if (offerOutcomes != null && !offerOutcomes.Contains("accepted", StringComparer.OrdinalIgnoreCase))
					yield return new(AdmissionWorkflowErrorCodes.InvalidCondition,
						$"Stage '{source!.Name}' cannot use a usual next step while Accepted is disabled. Add a special outcome path or enable Accepted.",
						AdmissionWorkflowValidationSeverity.Error, route.FromStageId, route.Id);
				continue;
			}
			var rule = ReadSimpleRule(route.ConditionRuleJson);
			if (rule == null) continue;
			var (field, operation, value) = rule.Value;
			if (!seen.Add((route.FromStageId, field, operation, value)))
				yield return new(AdmissionWorkflowErrorCodes.InvalidCondition,
					"This condition is already used by another path from the same stage. Edit that path or choose a different condition.",
					AdmissionWorkflowValidationSeverity.Error, route.FromStageId, route.Id);
			if (operation != "equals") continue;
			var allowed = source == null ? null : AllowedOutcomes(source, field);
			if (allowed != null && !allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
				yield return new(AdmissionWorkflowErrorCodes.InvalidCondition,
					$"Stage '{source!.Name}' cannot complete with '{value}'. Choose an allowed outcome or update its completion settings.",
					AdmissionWorkflowValidationSeverity.Error, route.FromStageId, route.Id);
		}
	}

	private static (string Field, string Operator, string Value)? ReadSimpleRule(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		try
		{
			using var document = JsonDocument.Parse(json);
			var node = document.RootElement;
			if (node.ValueKind != JsonValueKind.Object || node.EnumerateObject().Any(item => item.Name is not ("field" or "operator" or "value")) ||
				!node.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String ||
				!node.TryGetProperty("operator", out var operation) || operation.ValueKind != JsonValueKind.String) return null;
			var op = operation.GetString()!.ToLowerInvariant();
			var expected = node.TryGetProperty("value", out var value) ? value : default;
			if (expected.ValueKind is JsonValueKind.Array or JsonValueKind.Object) return null;
			return (field.GetString()!.ToLowerInvariant(), op, op is "isempty" or "isnotempty" ? "" : expected.ToString().ToLowerInvariant());
		}
		catch (JsonException) { return null; }
	}

	private static IEnumerable<string>? AllowedOutcomes(AdmissionWorkflowStageDefinition stage, string field)
	{
		try
		{
			if (stage.StageType == AdmissionWorkflowStageType.Payment && field == "payment.status" && AdmissionPaymentStagePolicy.Read(stage.ConfigurationJson) != null)
				return ["settled", "waived"];
			if (stage.StageType == AdmissionWorkflowStageType.Offer && field == "offer.status")
				return AdmissionOfferStagePolicy.Read(stage.ConfigurationJson)?.AllowedCompletionOutcomes;
			if (stage.StageType == AdmissionWorkflowStageType.Seat && field == "seat.status")
				return AdmissionSeatStagePolicy.Read(stage.ConfigurationJson)?.AllowedCompletionOutcomes;
			return null;
		}
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException) { return null; }
	}
}
