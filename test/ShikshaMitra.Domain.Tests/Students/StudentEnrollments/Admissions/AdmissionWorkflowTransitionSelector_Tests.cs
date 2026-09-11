using System;
using System.Collections.Generic;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowTransitionSelector_Tests
{
	private readonly AdmissionWorkflowTransitionSelector _selector =
		new(new AdmissionWorkflowConditionEvaluator());

	[Fact]
	public void Should_Select_Lowest_Priority_Eligible_Conditional_Transition()
	{
		var transitions = CreateTransitions(
			Transition(20, Condition("application.score", "GreaterThanOrEqual", 60)),
			Transition(10, Condition("application.score", "GreaterThanOrEqual", 80)),
			Transition(30, isDefault: true));

		var selected = _selector.Select(
			transitions,
			new Dictionary<string, string> { ["application.score"] = "85" });

		selected.Priority.ShouldBe(10);
	}

	[Fact]
	public void Should_Use_Default_When_No_Conditional_Transition_Is_Eligible()
	{
		var transitions = CreateTransitions(
			Transition(10, Condition("application.score", "GreaterThanOrEqual", 80)),
			Transition(20, isDefault: true));

		var selected = _selector.Select(
			transitions,
			new Dictionary<string, string> { ["application.score"] = "65" });

		selected.IsDefault.ShouldBeTrue();
	}

	[Fact]
	public void Should_Reject_Ambiguous_Eligible_Transitions_With_The_Same_Priority()
	{
		var transitions = CreateTransitions(
			Transition(10, Condition("application.score", "GreaterThanOrEqual", 60)),
			Transition(10, Condition("application.score", "LessThan", 90)));

		var exception = Should.Throw<BusinessException>(() => _selector.Select(
			transitions,
			new Dictionary<string, string> { ["application.score"] = "75" }));

		exception.Code.ShouldBe(AdmissionApplicationErrorCodes.AmbiguousTransition);
	}

	[Fact]
	public void Should_Reject_When_No_Transition_Is_Eligible_And_No_Default_Exists()
	{
		var transitions = CreateTransitions(
			Transition(10, Condition("application.score", "GreaterThanOrEqual", 80)));

		var exception = Should.Throw<BusinessException>(() => _selector.Select(
			transitions,
			new Dictionary<string, string> { ["application.score"] = "65" }));

		exception.Code.ShouldBe(AdmissionApplicationErrorCodes.NoEligibleTransition);
	}

	[Fact]
	public void Admin_Override_Should_Select_The_Requested_Transition_Without_Evaluating_Its_Condition()
	{
		var transitions = CreateTransitions(
			Transition(10, Condition("application.score", "GreaterThanOrEqual", 80)));
		var requested = transitions[0];

		var selected = _selector.Select(
			transitions,
			new Dictionary<string, string> { ["application.score"] = "65" },
			requested.Id,
			adminOverride: true);

		selected.Id.ShouldBe(requested.Id);
	}

	private static List<AdmissionWorkflowTransitionDefinition> CreateTransitions(
		params TransitionSpec[] definitions)
	{
		var workflowId = Guid.NewGuid();
		var fromStageId = Guid.NewGuid();
		var transitions = new List<AdmissionWorkflowTransitionDefinition>();
		foreach (var definition in definitions)
		{
			transitions.Add(new AdmissionWorkflowTransitionDefinition(
				Guid.NewGuid(),
				workflowId,
				fromStageId,
				Guid.NewGuid(),
				definition.Priority,
				definition.Condition,
				definition.IsDefault));
		}

		return transitions;
	}

	private static TransitionSpec Transition(int priority, string? condition = null, bool isDefault = false) =>
		new(priority, condition, isDefault);

	private static string Condition(string field, string @operator, int value) =>
		$$"""{ "operator": "{{@operator}}", "field": "{{field}}", "value": {{value}} }""";

	private sealed record TransitionSpec(int Priority, string? Condition, bool IsDefault);
}
