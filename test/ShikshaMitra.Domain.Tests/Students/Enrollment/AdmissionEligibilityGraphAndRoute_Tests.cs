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
	[InlineData(null, false)]
	[InlineData("{\"eligibility\":{\"enabled\":false}}", false)]
	[InlineData("{\"eligibility\":{\"version\":2,\"enabled\":true,\"criteria\":[]}}", true)]
	[InlineData("{\"eligibility\":{\"version\":1,\"enabled\":true,\"criteria\":[]}}", true)]
	public void Eligibility_graph_accepts_legacy_and_rejects_invalid_managed_policy(string? json, bool invalid)
	{
		var workflow = Guid.NewGuid();
		var entry = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow, "ENTRY", "Entry", AdmissionWorkflowStageType.Custom, 0, true, false, null, null, true);
		var eligibility = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow, "ELIG", "Eligibility", AdmissionWorkflowStageType.Eligibility, 1, true, false, null, json, false, true);
		var transition = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, entry.Id, eligibility.Id, 0, isDefault: true);
		new AdmissionWorkflowGraphValidator().Validate([entry, eligibility], [transition]).Any(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error).ShouldBe(invalid);
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
