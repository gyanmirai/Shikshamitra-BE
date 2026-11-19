using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEligibilityGraphAndRoute_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{\"eligibility\":{\"enabled\":false}}")]
	[InlineData("{\"eligibility\":{\"version\":2,\"enabled\":true,\"criteria\":[]}}")]
	[InlineData("{\"eligibility\":{\"version\":1,\"enabled\":true,\"criteria\":[]}}")]
	public void Publication_rejects_retired_eligibility_stages_regardless_of_legacy_policy(string? json)
	{
		var workflow = Guid.NewGuid();
		var entry = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow, "ENTRY", "Entry", AdmissionWorkflowStageType.Assessment, 0, true, false, null, null, true);
		var eligibility = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow, "ELIG", "Eligibility", AdmissionWorkflowStageType.Eligibility, 1, true, false, null, json, false, true);
		var transition = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, entry.Id, eligibility.Id, 0, isDefault: true);
		new AdmissionWorkflowGraphValidator().Validate([entry, eligibility], [transition]).ShouldContain(issue =>
			issue.Code == AdmissionWorkflowErrorCodes.InvalidTransition && issue.StageId == eligibility.Id &&
			issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}
	[Theory]
	[InlineData("ineligible")]
	[InlineData("needsInformation")]
	public void Eligibility_negative_outcomes_require_the_selected_route_to_depend_on_that_result(string status)
	{
		var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["eligibility.status"] = status, ["application.status"] = "Submitted" };
		Should.Throw<BusinessException>(() => AdmissionEligibilityEvidence.EnsureRoute(status, Route(null, true), facts));
		Should.Throw<BusinessException>(() => AdmissionEligibilityEvidence.EnsureRoute(status, null, facts));
		var match = "{\"operator\":\"Equals\",\"field\":\"eligibility.status\",\"value\":\"" + status + "\"}";
		AdmissionEligibilityEvidence.EnsureRoute(status, Route(match), facts);
		var generic = "{\"operator\":\"Equals\",\"field\":\"application.status\",\"value\":\"Submitted\"}";
		Should.Throw<BusinessException>(() => AdmissionEligibilityEvidence.EnsureRoute(status,
			Route("{\"operator\":\"ANY\",\"conditions\":[" + match + "," + generic + "]}"), facts));
		AdmissionEligibilityEvidence.EnsureRoute(status, Route("{\"operator\":\"ALL\",\"conditions\":[" + match + "," + generic + "]}"), facts);
	}
	private static AdmissionWorkflowTransitionDefinition Route(string? json, bool isDefault = false) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, json, isDefault);
}
