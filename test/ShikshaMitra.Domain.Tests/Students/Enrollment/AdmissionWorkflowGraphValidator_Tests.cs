using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionWorkflowGraphValidator_Tests
{
	private readonly AdmissionWorkflowGraphValidator _validator = new();

	[Fact]
	public void ValidLinearWorkflowPassesWithoutUsingDisplayOrderAsBusinessLogic()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "INQUIRY", 100, isEntry: true);
		var form = Stage(workflowId, "FORM", 0);
		var terminal = Stage(workflowId, "ENROLLED", 50, isTerminal: true,
			type: AdmissionWorkflowStageType.Enrollment);
		var transitions = new[]
		{
			Transition(workflowId, entry.Id, form.Id, 1),
			Transition(workflowId, form.Id, terminal.Id, 1)
		};

		var issues = _validator.Validate([entry, form, terminal], transitions);

		issues.ShouldNotContain(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error);
		issues.ShouldNotContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.RuntimeHandlerUnavailable &&
			issue.StageId == terminal.Id);
	}

	[Fact]
	public void RequiresExactlyOneExplicitEntryAndAtLeastOneTerminal()
	{
		var workflowId = Guid.NewGuid();
		var first = Stage(workflowId, "FIRST", 0, isEntry: true);
		var second = Stage(workflowId, "SECOND", 1, isEntry: true);

		var issues = _validator.Validate([first, second], [Transition(workflowId, first.Id, second.Id, 1)]);

		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidInitialStage);
		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidFinalStage);
	}

	[Fact]
	public void DetectsOrphansDeadEndsAndUnreachableStages()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var terminal = Stage(workflowId, "TERMINAL", 1, isTerminal: true);
		var orphan = Stage(workflowId, "ORPHAN", 2);

		var issues = _validator.Validate(
			[entry, terminal, orphan],
			[Transition(workflowId, entry.Id, terminal.Id, 1)]);

		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.OrphanStage && issue.StageId == orphan.Id);
		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.DeadEndStage && issue.StageId == orphan.Id);
		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.UnreachableStage && issue.StageId == orphan.Id);
	}

	[Fact]
	public void DetectsCycles()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var review = Stage(workflowId, "REVIEW", 1);
		var terminal = Stage(workflowId, "TERMINAL", 2, isTerminal: true);
		var transitions = new[]
		{
			Transition(workflowId, entry.Id, review.Id, 1),
			Transition(workflowId, review.Id, entry.Id, 1),
			Transition(workflowId, review.Id, terminal.Id, 2)
		};

		var issues = _validator.Validate([entry, review, terminal], transitions);

		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.CycleDetected);
	}

	[Fact]
	public void RejectsDuplicateDefaultTransitionsAndExecutableConditionText()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var approved = Stage(workflowId, "APPROVED", 1, isTerminal: true);
		var rejected = Stage(workflowId, "REJECTED", 2, isTerminal: true);
		var transitions = new[]
		{
			Transition(workflowId, entry.Id, approved.Id, 1, isDefault: true),
			Transition(workflowId, entry.Id, rejected.Id, 2, isDefault: true),
			Transition(workflowId, entry.Id, rejected.Id, 3,
				condition: "{\"operator\":\"eval(application.code)\",\"field\":\"application.code\",\"value\":true}")
		};

		var issues = _validator.Validate([entry, approved, rejected], transitions);

		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.DuplicateDefaultTransition);
		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidCondition);
	}

	[Fact]
	public void AcceptsControlledNestedConditions()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var terminal = Stage(workflowId, "TERMINAL", 1, isTerminal: true);
		var condition = """
			{
			  "operator": "ALL",
			  "conditions": [
			    { "field": "assessment.result", "operator": "Equals", "value": "Qualified" }
			  ]
			}
			""";

		var issues = _validator.Validate(
			[entry, terminal],
			[Transition(workflowId, entry.Id, terminal.Id, 1, condition)]);

		issues.Where(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidCondition).ShouldBeEmpty();
	}

	[Fact]
	public void RejectsConditionFieldsOutsideTheControlledRegistry()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var terminal = Stage(workflowId, "TERMINAL", 1, isTerminal: true);
		const string condition =
			"""{ "field": "unknown.moduleValue", "operator": "Equals", "value": "yes" }""";

		var issues = _validator.Validate(
			[entry, terminal],
			[Transition(workflowId, entry.Id, terminal.Id, 1, condition)]);

		issues.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidCondition);
	}

	[Theory]
	[InlineData(AdmissionWorkflowStageType.Payment)]
	[InlineData(AdmissionWorkflowStageType.Offer)]
	[InlineData(AdmissionWorkflowStageType.Seat)]
	public void Optional_managed_configuration_preserves_basic_stage_behavior(AdmissionWorkflowStageType type)
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var optional = Stage(workflowId, type.ToString().ToUpperInvariant(), 1, type: type);
		var terminal = Stage(workflowId, "TERMINAL", 2, isTerminal: true);

		var issues = _validator.Validate([entry, optional, terminal],
			[Transition(workflowId, entry.Id, optional.Id, 1), Transition(workflowId, optional.Id, terminal.Id, 1)]);

		issues.ShouldNotContain(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}

	private static AdmissionWorkflowStageDefinition Stage(
		Guid workflowId,
		string code,
		int displayOrder,
		bool isEntry = false,
		bool isTerminal = false,
		AdmissionWorkflowStageType type = AdmissionWorkflowStageType.Custom) =>
		new(Guid.NewGuid(), workflowId, code, code, type, displayOrder, true, false, null, null,
			isEntry, isTerminal);

	private static AdmissionWorkflowTransitionDefinition Transition(
		Guid workflowId,
		Guid fromStageId,
		Guid toStageId,
		int priority,
		string? condition = null,
		bool isDefault = false) =>
		new(Guid.NewGuid(), workflowId, fromStageId, toStageId, priority, condition, isDefault);
}
