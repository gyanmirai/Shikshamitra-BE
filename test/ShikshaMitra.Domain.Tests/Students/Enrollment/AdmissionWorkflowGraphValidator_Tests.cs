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
	public void New_workflows_offer_only_supported_structured_stage_types()
	{
		var selectable = AdmissionWorkflowStageTypeRegistry.All
			.Where(stage => stage.IsSelectable)
			.Select(stage => stage.Type)
			.ToList();

		selectable.ShouldNotContain(AdmissionWorkflowStageType.Seat);
		selectable.ShouldNotContain(AdmissionWorkflowStageType.Enrollment);
		selectable.ShouldNotContain(AdmissionWorkflowStageType.Custom);
		selectable.ShouldNotContain(AdmissionWorkflowStageType.Appointment);
		selectable.ShouldContain(AdmissionWorkflowStageType.Form);
		selectable.ShouldContain(AdmissionWorkflowStageType.Review);
		selectable.ShouldContain(AdmissionWorkflowStageType.Approval);
		AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Approval)!
			.DisplayName.ShouldBe("Admission decision");
	}

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
	public void Any_stage_type_can_be_selected_as_the_single_entry()
	{
		AdmissionWorkflowStageTypeRegistry.All.ShouldAllBe(metadata => metadata.CanBeEntryStage);
		var workflowId = Guid.NewGuid();
		var assessment = Stage(workflowId, "ASSESSMENT", 0, isEntry: true,
			type: AdmissionWorkflowStageType.Assessment);
		var form = Stage(workflowId, "FORM", 1, type: AdmissionWorkflowStageType.Form,
			configurationJson: """{"formDefinitionId":"00000000-0000-0000-0000-000000000001","form":{"enabled":true,"version":1}}""");
		var decision = Stage(workflowId, "DECISION", 2, isTerminal: true,
			type: AdmissionWorkflowStageType.Approval);

		var issues = _validator.Validate([assessment, form, decision],
			[Transition(workflowId, assessment.Id, form.Id, 0),
			 Transition(workflowId, form.Id, decision.Id, 0)]);

		issues.ShouldNotContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidInitialStage);
		issues.ShouldNotContain(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error);
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
	public void All_stage_types_allow_user_selected_endings()
	{
		AdmissionWorkflowStageTypeRegistry.All.ShouldAllBe(metadata => metadata.CanBeTerminalStage);
	}

	[Theory]
	[InlineData(AdmissionWorkflowStageType.Form)]
	[InlineData(AdmissionWorkflowStageType.Assessment)]
	[InlineData(AdmissionWorkflowStageType.Payment)]
	[InlineData(AdmissionWorkflowStageType.DocumentVerification)]
	public void Assessment_can_lead_directly_to_any_user_selected_ending(AdmissionWorkflowStageType terminalType)
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRANCE_EXAM", 0, isEntry: true,
			type: AdmissionWorkflowStageType.Assessment);
		var terminal = Stage(workflowId, "REGISTRATION", 1, isTerminal: true, type: terminalType,
			configurationJson: terminalType == AdmissionWorkflowStageType.Form
				? """{"formDefinitionId":"00000000-0000-0000-0000-000000000001","form":{"enabled":true,"version":1}}""" : null);

		var issues = _validator.Validate([entry, terminal], [Transition(workflowId, entry.Id, terminal.Id, 0)]);

		issues.ShouldNotContain(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}

	[Fact]
	public void Single_form_can_be_both_entry_and_ending()
	{
		var form = Stage(Guid.NewGuid(), "REGISTRATION", 0, isEntry: true, isTerminal: true,
			type: AdmissionWorkflowStageType.Form,
			configurationJson: """{"formDefinitionId":"00000000-0000-0000-0000-000000000001","form":{"enabled":true,"version":1}}""");

		_validator.Validate([form], []).ShouldNotContain(issue =>
			issue.Severity == AdmissionWorkflowValidationSeverity.Error);
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
	public void Rejects_ambiguous_priority_and_unconditional_non_default_routes()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var approved = Stage(workflowId, "APPROVED", 1, isTerminal: true);
		var rejected = Stage(workflowId, "REJECTED", 2, isTerminal: true);
		var condition = """{ "field": "review.decision", "operator": "Equals", "value": "approve" }""";
		var conditional = Transition(workflowId, entry.Id, approved.Id, 0, condition);
		var duplicatePriority = Transition(workflowId, entry.Id, rejected.Id, 0, condition);
		var unconditional = new AdmissionWorkflowTransitionDefinition(
			Guid.NewGuid(), workflowId, entry.Id, rejected.Id, 1, null, false);

		var issues = _validator.Validate(
			[entry, approved, rejected],
			[conditional, duplicatePriority, unconditional]);

		issues.ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.DuplicateTransitionPriority &&
			issue.StageId == entry.Id);
		issues.ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.InvalidCondition &&
			issue.TransitionId == unconditional.Id);
	}

	[Fact]
	public void Rejects_negative_transition_priority()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var terminal = Stage(workflowId, "TERMINAL", 1, isTerminal: true);
		var transition = Transition(workflowId, entry.Id, terminal.Id, -1, isDefault: true);

		var issues = _validator.Validate([entry, terminal], [transition]);

		issues.ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.InvalidTransition &&
			issue.TransitionId == transition.Id);
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

	[Theory]
	[InlineData(AdmissionWorkflowStageType.Form,
		"{\"form\":{\"enabled\":true,\"version\":1,\"responsibility\":\"nobody\"}}",
		AdmissionFormStageErrorCodes.InvalidPolicy)]
	[InlineData(AdmissionWorkflowStageType.Review,
		"{\"review\":{\"enabled\":true,\"version\":2}}",
		AdmissionReviewErrorCodes.InvalidPolicy)]
	[InlineData(AdmissionWorkflowStageType.Payment,
		"{\"payment\":{\"enabled\":true,\"version\":1}}",
		AdmissionPaymentStageErrorCodes.InvalidPolicy)]
	[InlineData(AdmissionWorkflowStageType.Offer,
		"{\"offer\":{\"enabled\":true,\"version\":1,\"validityDays\":0}}",
		AdmissionOfferStageErrorCodes.InvalidPolicy)]
	[InlineData(AdmissionWorkflowStageType.Seat,
		"{\"seat\":{\"enabled\":true,\"version\":1,\"defaultCapacity\":0}}",
		AdmissionSeatStageErrorCodes.InvalidPolicy)]
	public void Rejects_managed_configuration_that_runtime_would_reject(
		AdmissionWorkflowStageType type,
		string configurationJson,
		string expectedCode)
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var managed = Stage(workflowId, type.ToString().ToUpperInvariant(), 1,
			type: type, configurationJson: configurationJson);
		var terminal = Stage(workflowId, "TERMINAL", 2, isTerminal: true);

		var issues = _validator.Validate([entry, managed, terminal],
			[Transition(workflowId, entry.Id, managed.Id, 1), Transition(workflowId, managed.Id, terminal.Id, 1)]);

		issues.ShouldContain(issue =>
			issue.Code == expectedCode &&
			issue.StageId == managed.Id &&
			issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}

	[Fact]
	public void Required_stage_cannot_allow_an_ordinary_manual_skip()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true, allowManualSkip: true);
		var terminal = Stage(workflowId, "TERMINAL", 1, isTerminal: true);

		var issues = _validator.Validate(
			[entry, terminal],
			[Transition(workflowId, entry.Id, terminal.Id, 1)]);

		issues.ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.RequiredStageCannotAllowSkip &&
			issue.StageId == entry.Id &&
			issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}

	[Fact]
	public void Runtime_unavailable_stage_blocks_publication()
	{
		var workflowId = Guid.NewGuid();
		var entry = Stage(workflowId, "ENTRY", 0, isEntry: true);
		var appointment = Stage(workflowId, "APPOINTMENT", 1,
			isTerminal: true, type: AdmissionWorkflowStageType.Appointment);

		var issues = _validator.Validate(
			[entry, appointment],
			[Transition(workflowId, entry.Id, appointment.Id, 1)]);

		issues.ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.RuntimeHandlerUnavailable &&
			issue.StageId == appointment.Id &&
			issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}

	private static AdmissionWorkflowStageDefinition Stage(
		Guid workflowId,
		string code,
		int displayOrder,
		bool isEntry = false,
		bool isTerminal = false,
		AdmissionWorkflowStageType type = AdmissionWorkflowStageType.Custom,
		bool isRequired = true,
		bool allowManualSkip = false,
		string? configurationJson = null) =>
		new(Guid.NewGuid(), workflowId, code, code, type, displayOrder, isRequired, allowManualSkip, null, configurationJson,
			isEntry, isTerminal);

	private static AdmissionWorkflowTransitionDefinition Transition(
		Guid workflowId,
		Guid fromStageId,
		Guid toStageId,
		int priority,
		string? condition = null,
		bool isDefault = false) =>
		new(Guid.NewGuid(), workflowId, fromStageId, toStageId, priority, condition,
			isDefault || string.IsNullOrWhiteSpace(condition));
}
