using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentEvidence_Tests
{
	[Fact]
	public void Failed_fractional_score_is_exact_and_cannot_take_a_default_route()
	{
		var attempt = Attempt(false);
		attempt.RecordResult(39.999m, null, null, Guid.NewGuid(), Policy(false));
		var facts = AdmissionAssessmentEvidence.GetFacts(attempt);
		facts["assessment.marks"].ShouldBe("39.999");
		facts["assessment.outcome"].ShouldBe("failed");
		var transition = Transition(null, true);
		Should.Throw<BusinessException>(() => AdmissionAssessmentEvidence.EnsureRoute(attempt, transition, facts))
			.Code.ShouldBe(AdmissionAssessmentErrorCodes.RouteRequired);
	}

	[Fact]
	public void Explicit_failed_route_must_also_match_its_application_condition()
	{
		var attempt = Attempt(false);
		attempt.RecordResult(20, null, null, Guid.NewGuid(), Policy(false));
		var facts = AdmissionAssessmentEvidence.GetFacts(attempt);
		var route = Transition("""{"operator":"ALL","conditions":[{"field":"assessment.outcome","operator":"Equals","value":"failed"},{"field":"application.status","operator":"Equals","value":"Draft"}]}""", false);
		facts["application.status"] = "Submitted";
		AdmissionAssessmentEvidence.HasExplicitOutcomeRoute(route, attempt, facts).ShouldBeFalse();
		facts["application.status"] = "Draft";
		AdmissionAssessmentEvidence.HasExplicitOutcomeRoute(route, attempt, facts).ShouldBeTrue();
	}

	[Fact]
	public void Unreviewed_result_does_not_expose_authoritative_marks_or_a_passed_outcome()
	{
		var attempt = Attempt(true);
		attempt.RecordResult(90, null, null, Guid.NewGuid(), Policy(true));
		var facts = AdmissionAssessmentEvidence.GetFacts(attempt);
		facts["assessment.outcome"].ShouldBe("pending");
		facts.ContainsKey("assessment.marks").ShouldBeFalse();
		Should.Throw<BusinessException>(() => AdmissionAssessmentEvidence.EnsureComplete(attempt))
			.Code.ShouldBe(AdmissionAssessmentErrorCodes.EvidenceRequired);
	}

	private static AdmissionAssessmentAttempt Attempt(bool review) => new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), null, 1, "{}");
	private static AdmissionAssessmentPolicy Policy(bool review) => new()
	{
		Version = 1, Enabled = true, ScoringEnabled = true, TotalMarks = 100, PassingMarks = 40, MaxAttempts = 2, ReviewRequired = review
	};
	private static AdmissionWorkflowTransitionDefinition Transition(string? condition, bool isDefault) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, condition, isDefault);
}
