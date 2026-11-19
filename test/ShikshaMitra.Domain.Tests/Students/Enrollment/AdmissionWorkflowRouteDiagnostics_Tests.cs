using System;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowRouteDiagnostics_Tests
{
	[Fact]
	public void Offer_without_accepted_completion_requires_a_special_outcome_path()
	{
		var workflow = Guid.NewGuid();
		var policy = new AdmissionOfferStagePolicy { Version = 1, Enabled = true, Title = "Offer", Message = "Admission offer",
			AllowedCompletionOutcomes = ["declined"] };
		var stage = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow, "OFFER", "Offer", AdmissionWorkflowStageType.Offer,
			0, true, false, null, JsonSerializer.Serialize(new { offer = policy }));
		var route = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, stage.Id, Guid.NewGuid(), 0, null, true);
		AdmissionWorkflowRouteDiagnostics.Validate([stage], [route]).ShouldContain(item => item.Message.Contains("usual next step"));
	}

	[Fact]
	public void Identical_simple_conditions_are_rejected_even_with_different_priorities()
	{
		var workflow = Guid.NewGuid();
		var source = Guid.NewGuid();
		var first = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, source, Guid.NewGuid(), 0,
			"{\"field\":\"application.CATEGORY\",\"operator\":\"Equals\",\"value\":\"Transfer\"}");
		var second = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, source, Guid.NewGuid(), 1,
			"{\"value\":\"transfer\",\"operator\":\"equals\",\"field\":\"application.category\"}");
		var issues = AdmissionWorkflowRouteDiagnostics.Validate([], [first, second]).ToList();
		issues.Count.ShouldBe(1);
		issues.Single().TransitionId.ShouldBe(second.Id);
		issues.Single().Severity.ShouldBe(AdmissionWorkflowValidationSeverity.Error);
	}

	[Fact]
	public void Different_comparisons_are_not_treated_as_duplicate_conditions()
	{
		var workflow = Guid.NewGuid();
		var source = Guid.NewGuid();
		var routes = new[] { "GreaterThan", "LessThan" }.Select((op, priority) =>
			new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, source, Guid.NewGuid(), priority,
				JsonSerializer.Serialize(new { field = "application.MARKS", @operator = op, value = "40" }))).ToList();
		AdmissionWorkflowRouteDiagnostics.Validate([], routes).ShouldBeEmpty();
	}

	[Fact]
	public void Offer_path_cannot_use_an_outcome_disabled_in_the_source_stage()
	{
		var workflow = Guid.NewGuid();
		var policy = new AdmissionOfferStagePolicy { Version = 1, Enabled = true, Title = "Offer", Message = "Admission offer",
			AllowedCompletionOutcomes = ["accepted"] };
		var stage = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow, "OFFER", "Offer", AdmissionWorkflowStageType.Offer,
			0, true, false, null, JsonSerializer.Serialize(new { offer = policy }));
		var route = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, stage.Id, Guid.NewGuid(), 0,
			"{\"field\":\"offer.status\",\"operator\":\"Equals\",\"value\":\"expired\"}");
		AdmissionWorkflowRouteDiagnostics.Validate([stage], [route]).ShouldContain(item => item.Message.Contains("cannot complete"));
	}
}
