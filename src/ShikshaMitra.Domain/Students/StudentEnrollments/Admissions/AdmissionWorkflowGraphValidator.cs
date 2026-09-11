using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionWorkflowValidationIssue(
	string Code,
	string Message,
	AdmissionWorkflowValidationSeverity Severity = AdmissionWorkflowValidationSeverity.Error,
	Guid? StageId = null,
	Guid? TransitionId = null);

public class AdmissionWorkflowGraphValidator : DomainService
{
	private static readonly HashSet<string> GroupOperators = new(StringComparer.OrdinalIgnoreCase)
	{
		"ALL",
		"ANY"
	};

	private static readonly HashSet<string> ConditionOperators = new(StringComparer.OrdinalIgnoreCase)
	{
		"Equals",
		"NotEquals",
		"GreaterThan",
		"GreaterThanOrEqual",
		"LessThan",
		"LessThanOrEqual",
		"Contains",
		"NotContains",
		"StartsWith",
		"EndsWith",
		"IsEmpty",
		"IsNotEmpty",
		"In",
		"NotIn"
	};

	public IReadOnlyList<AdmissionWorkflowValidationIssue> Validate(
		IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages,
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> transitions)
	{
		var issues = new List<AdmissionWorkflowValidationIssue>();
		if (stages.Count == 0)
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.NoStages, "Add at least one stage before publishing."));
			return issues;
		}

		ValidateStages(stages, issues);
		var validTransitions = ValidateTransitions(stages, transitions, issues);
		ValidateGraph(stages, validTransitions, issues);
		issues.AddRange(AdmissionInquiryGraphRules.Validate(stages));
		issues.AddRange(AdmissionReviewGraphRules.Validate(stages, validTransitions));
		return issues;
	}

	private static void ValidateStages(
		IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages,
		ICollection<AdmissionWorkflowValidationIssue> issues)
	{
		foreach (var duplicate in stages.GroupBy(stage => stage.Code, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.DuplicateStageCode, $"Stage code '{duplicate.Key}' is used more than once.", duplicate.First().Id));
		}

		var entryStages = stages.Where(stage => stage.IsEntryStage).ToList();
		if (entryStages.Count != 1)
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidInitialStage, "Select exactly one entry stage."));
		}

		var terminalStages = stages.Where(stage => stage.IsTerminalStage).ToList();
		if (terminalStages.Count == 0)
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidFinalStage, "Select at least one terminal stage."));
		}

		foreach (var stage in stages)
		{
			var metadata = AdmissionWorkflowStageTypeRegistry.Find(stage.StageType);
			if (metadata == null)
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidTransition, $"Stage '{stage.Name}' uses an unsupported stage type.", stage.Id));
				continue;
			}

			if (stage.IsEntryStage && !metadata.CanBeEntryStage)
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidInitialStage, $"{metadata.DisplayName} cannot be used as an entry stage.", stage.Id));
			}

			if (stage.IsTerminalStage && !metadata.CanBeTerminalStage)
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidFinalStage, $"{metadata.DisplayName} cannot be used as a terminal stage.", stage.Id));
			}

			if (!metadata.IsRuntimeReady)
			{
				issues.Add(new AdmissionWorkflowValidationIssue(
					AdmissionWorkflowErrorCodes.RuntimeHandlerUnavailable,
					$"Stage '{stage.Name}' can be configured, but its runtime handler is not available yet.",
					AdmissionWorkflowValidationSeverity.Warning,
					stage.Id));
			}

			ValidateStageConfiguration(stage, metadata, issues);
		}
	}

	private static List<AdmissionWorkflowTransitionDefinition> ValidateTransitions(
		IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages,
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> transitions,
		ICollection<AdmissionWorkflowValidationIssue> issues)
	{
		var stageIds = stages.Select(stage => stage.Id).ToHashSet();
		var validTransitions = new List<AdmissionWorkflowTransitionDefinition>();

		foreach (var duplicate in transitions.GroupBy(transition => new { transition.FromStageId, transition.ToStageId }).Where(group => group.Count() > 1))
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.DuplicateTransition, "The same transition is configured more than once.", transitionId: duplicate.First().Id));
		}

		foreach (var defaults in transitions.Where(transition => transition.IsDefault).GroupBy(transition => transition.FromStageId).Where(group => group.Count() > 1))
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.DuplicateDefaultTransition, "A stage can have only one default transition.", defaults.Key, defaults.First().Id));
		}

		foreach (var transition in transitions)
		{
			if (!stageIds.Contains(transition.FromStageId) || !stageIds.Contains(transition.ToStageId))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidTransition, "Transition references a stage outside this workflow.", transitionId: transition.Id));
				continue;
			}

			if (transition.FromStageId == transition.ToStageId)
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidTransition, "A transition cannot return to the same stage.", transition.FromStageId, transition.Id));
				continue;
			}

			if (transition.IsDefault && !string.IsNullOrWhiteSpace(transition.ConditionRuleJson))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidCondition, "A default transition cannot also have a condition.", transition.FromStageId, transition.Id));
			}

			ValidateCondition(transition, issues);
			validTransitions.Add(transition);
		}

		return validTransitions;
	}

	private static void ValidateGraph(
		IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages,
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> transitions,
		ICollection<AdmissionWorkflowValidationIssue> issues)
	{
		var entryStages = stages.Where(stage => stage.IsEntryStage).ToList();
		var entryStage = entryStages.Count == 1 ? entryStages[0] : null;
		var terminalIds = stages.Where(stage => stage.IsTerminalStage).Select(stage => stage.Id).ToHashSet();
		var outgoing = transitions.GroupBy(transition => transition.FromStageId).ToDictionary(group => group.Key, group => group.Select(transition => transition.ToStageId).ToList());
		var incoming = transitions.GroupBy(transition => transition.ToStageId).ToDictionary(group => group.Key, group => group.Select(transition => transition.FromStageId).ToList());

		foreach (var stage in stages)
		{
			if (stage.IsEntryStage && incoming.ContainsKey(stage.Id))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidInitialStage, "The entry stage cannot have incoming transitions.", stage.Id));
			}

			if (!stage.IsEntryStage && !incoming.ContainsKey(stage.Id))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.OrphanStage, $"Stage '{stage.Name}' has no incoming transition.", stage.Id));
			}

			if (stage.IsTerminalStage && outgoing.ContainsKey(stage.Id))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidFinalStage, $"Terminal stage '{stage.Name}' cannot have outgoing transitions.", stage.Id));
			}

			if (!stage.IsTerminalStage && !outgoing.ContainsKey(stage.Id))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.DeadEndStage, $"Stage '{stage.Name}' has no outgoing transition.", stage.Id));
			}
		}

		if (entryStage == null || terminalIds.Count == 0)
		{
			return;
		}

		var reachableFromEntry = Traverse(entryStage.Id, outgoing);
		foreach (var stage in stages.Where(stage => !reachableFromEntry.Contains(stage.Id)))
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.UnreachableStage, $"Stage '{stage.Name}' cannot be reached from the entry stage.", stage.Id));
		}

		var canReachTerminal = new HashSet<Guid>(terminalIds);
		var queue = new Queue<Guid>(terminalIds);
		while (queue.TryDequeue(out var stageId))
		{
			if (!incoming.TryGetValue(stageId, out var previousStages))
			{
				continue;
			}

			foreach (var previousStage in previousStages.Where(canReachTerminal.Add))
			{
				queue.Enqueue(previousStage);
			}
		}

		foreach (var stage in stages.Where(stage => reachableFromEntry.Contains(stage.Id) && !canReachTerminal.Contains(stage.Id)))
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.TerminalUnreachable, $"Stage '{stage.Name}' cannot reach a terminal stage.", stage.Id));
		}

		if (HasCycle(stages.Select(stage => stage.Id), outgoing))
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.CycleDetected, "Cyclic workflows are not supported."));
		}
	}

	private static void ValidateStageConfiguration(
		AdmissionWorkflowStageDefinition stage,
		AdmissionWorkflowStageTypeMetadata metadata,
		ICollection<AdmissionWorkflowValidationIssue> issues)
	{
		if (stage.StageType == AdmissionWorkflowStageType.Inquiry)
		{
			try { AdmissionInquiryPolicy.Read(stage.ConfigurationJson); }
			catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
			{
				issues.Add(Error(AdmissionInquiryManagementErrorCodes.InvalidPolicy, exception.Message, stage.Id));
				return;
			}
		}
		if (stage.StageType == AdmissionWorkflowStageType.Assessment)
		{
			try { AdmissionAssessmentPolicy.Read(stage.ConfigurationJson); }
			catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
			{
				issues.Add(Error(AdmissionAssessmentErrorCodes.InvalidPolicy, exception.Message, stage.Id));
				return;
			}
		}
		if (string.IsNullOrWhiteSpace(stage.ConfigurationJson))
		{
			if (metadata.SupportedConfiguration.Count > 0 && !UsesOptionalManagedConfiguration(stage.StageType))
			{
				issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidTransition, $"Stage '{stage.Name}' is missing required configuration.", stage.Id));
			}

			return;
		}

		try
		{
			if (stage.StageType == AdmissionWorkflowStageType.Eligibility)
			{
				try { AdmissionEligibilityPolicy.Read(stage.ConfigurationJson); }
				catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
				{
					issues.Add(Error(AdmissionEligibilityErrorCodes.InvalidPolicy, exception.Message, stage.Id));
					return;
				}
			}
			if (stage.StageType == AdmissionWorkflowStageType.DocumentVerification)
			{
				try { AdmissionDocumentPolicy.Read(stage.ConfigurationJson); }
				catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
				{
					issues.Add(Error(AdmissionDocumentErrorCodes.InvalidPolicy, exception.Message, stage.Id));
					return;
				}
			}
			using var document = JsonDocument.Parse(stage.ConfigurationJson);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				throw new JsonException();
			}

			foreach (var requiredProperty in metadata.SupportedConfiguration)
			{
				if (UsesOptionalManagedConfiguration(stage.StageType)) continue;
				if (!document.RootElement.TryGetProperty(requiredProperty, out var value) || value.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(value.ToString()))
				{
					issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidTransition, $"Stage '{stage.Name}' requires '{requiredProperty}'.", stage.Id));
				}
			}
		}
		catch (JsonException)
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidTransition, $"Stage '{stage.Name}' has invalid configuration JSON.", stage.Id));
		}
	}

	private static bool UsesOptionalManagedConfiguration(AdmissionWorkflowStageType type) => type is
		AdmissionWorkflowStageType.Inquiry or AdmissionWorkflowStageType.Assessment or
		AdmissionWorkflowStageType.DocumentVerification or AdmissionWorkflowStageType.Eligibility or
		AdmissionWorkflowStageType.Review or AdmissionWorkflowStageType.Payment or
		AdmissionWorkflowStageType.Offer or AdmissionWorkflowStageType.Seat;

	private static void ValidateCondition(
		AdmissionWorkflowTransitionDefinition transition,
		ICollection<AdmissionWorkflowValidationIssue> issues)
	{
		if (string.IsNullOrWhiteSpace(transition.ConditionRuleJson))
		{
			return;
		}

		try
		{
			using var document = JsonDocument.Parse(transition.ConditionRuleJson);
			ValidateConditionNode(document.RootElement);
		}
		catch (JsonException)
		{
			issues.Add(Error(AdmissionWorkflowErrorCodes.InvalidCondition, "Transition condition is not a valid controlled rule expression.", transition.FromStageId, transition.Id));
		}
	}

	private static void ValidateConditionNode(JsonElement node)
	{
		if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("operator", out var operatorProperty))
		{
			throw new JsonException();
		}

		var operatorName = operatorProperty.GetString();
		if (node.TryGetProperty("conditions", out var conditions))
		{
			if (operatorName == null || !GroupOperators.Contains(operatorName) || conditions.ValueKind != JsonValueKind.Array || conditions.GetArrayLength() == 0)
			{
				throw new JsonException();
			}

			foreach (var condition in conditions.EnumerateArray())
			{
				ValidateConditionNode(condition);
			}

			return;
		}

		if (operatorName == null || !ConditionOperators.Contains(operatorName) || !node.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(field.GetString()))
		{
			throw new JsonException();
		}
		if (!AdmissionWorkflowConditionFieldRegistry.IsSupported(field.GetString()!))
		{
			throw new JsonException();
		}

		if (!operatorName.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase) &&
			!operatorName.Equals("IsNotEmpty", StringComparison.OrdinalIgnoreCase) &&
			!node.TryGetProperty("value", out _))
		{
			throw new JsonException();
		}
	}

	private static HashSet<Guid> Traverse(Guid start, IReadOnlyDictionary<Guid, List<Guid>> edges)
	{
		var visited = new HashSet<Guid>();
		var queue = new Queue<Guid>();
		queue.Enqueue(start);
		while (queue.TryDequeue(out var current))
		{
			if (!visited.Add(current) || !edges.TryGetValue(current, out var nextStages))
			{
				continue;
			}

			foreach (var nextStage in nextStages)
			{
				queue.Enqueue(nextStage);
			}
		}

		return visited;
	}

	private static bool HasCycle(IEnumerable<Guid> stageIds, IReadOnlyDictionary<Guid, List<Guid>> edges)
	{
		var visiting = new HashSet<Guid>();
		var visited = new HashSet<Guid>();

		bool Visit(Guid stageId)
		{
			if (visiting.Contains(stageId))
			{
				return true;
			}

			if (!visited.Add(stageId))
			{
				return false;
			}

			visiting.Add(stageId);
			if (edges.TryGetValue(stageId, out var nextStages) && nextStages.Any(Visit))
			{
				return true;
			}

			visiting.Remove(stageId);
			return false;
		}

		return stageIds.Any(Visit);
	}

	private static AdmissionWorkflowValidationIssue Error(
		string code,
		string message,
		Guid? stageId = null,
		Guid? transitionId = null) =>
		new(code, message, AdmissionWorkflowValidationSeverity.Error, stageId, transitionId);
}
