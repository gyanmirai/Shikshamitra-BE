using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReviewGraphAndRoute_Tests
{
	[Theory]
	[InlineData("review.decision")]
	[InlineData("review.status")]
	[InlineData("review.invented")]
	public void Publication_rejects_retired_review_condition_fields(string field)
	{
		AdmissionWorkflowConditionFieldRegistry.IsSupported(field).ShouldBeFalse();
		var workflow = Guid.NewGuid(); var entry = Stage(workflow, "ENTRY", AdmissionWorkflowStageType.Assessment, null, true);
		var review = Stage(workflow, "REVIEW", AdmissionWorkflowStageType.Review, "{\"review\":{\"version\":1,\"enabled\":true}}");
		var end = Stage(workflow, "END", AdmissionWorkflowStageType.Approval, null, terminal: true);
		var condition = "{\"field\":\"" + field + "\",\"operator\":\"Equals\",\"value\":\"reject\"}";
		var links = new[] { Route(workflow, entry.Id, review.Id), new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow, review.Id, end.Id, 0, condition) };
		new AdmissionWorkflowGraphValidator().Validate([entry, review, end], links)
			.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidCondition);
	}
	[Theory]
	[InlineData(null)]
	[InlineData("{\"review\":{\"enabled\":false}}")]
	[InlineData("{\"review\":{\"enabled\":true,\"version\":1}}")]
	[InlineData("{\"review\":{\"enabled\":true,\"version\":2}}")]
	public void Publication_rejects_retired_review_stages_regardless_of_legacy_policy(string? policy)
	{
		var workflow = Guid.NewGuid(); var entry = Stage(workflow, "ENTRY", AdmissionWorkflowStageType.Assessment, null, true);
		var review = Stage(workflow, "REVIEW", AdmissionWorkflowStageType.Review, policy, terminal: true);
		new AdmissionWorkflowGraphValidator().Validate([entry, review], [Route(workflow, entry.Id, review.Id)])
			.ShouldContain(issue => issue.Code == AdmissionWorkflowErrorCodes.InvalidTransition &&
				issue.StageId == review.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}
	[Fact]
	public void Review_requires_earlier_managed_prerequisite_on_every_path()
	{
		var workflow = Guid.NewGuid(); var entry = Stage(workflow, "ENTRY", AdmissionWorkflowStageType.Custom, null, true);
		var source = Stage(workflow, "EXAM", AdmissionWorkflowStageType.Assessment, "{\"assessment\":{\"version\":1,\"enabled\":true,\"maxAttempts\":1}}");
		var review = Stage(workflow, "REVIEW", AdmissionWorkflowStageType.Review,
			"{\"review\":{\"version\":1,\"enabled\":true,\"prerequisites\":[{\"kind\":\"assessment\",\"sourceStageCode\":\"EXAM\"}]}}", terminal: true);
		var links = new List<AdmissionWorkflowTransitionDefinition> { Route(workflow, entry.Id, source.Id), Route(workflow, source.Id, review.Id) };
		AdmissionReviewGraphRules.Validate([entry, source, review], links).ShouldBeEmpty();
		links.Add(Route(workflow, entry.Id, review.Id));
		AdmissionReviewGraphRules.Validate([entry, source, review], links).ShouldContain(issue => issue.Code == AdmissionReviewErrorCodes.InvalidPolicy);
		AdmissionReviewGraphRules.Validate([entry, review], [Route(workflow, entry.Id, review.Id)])
			.ShouldContain(issue => issue.Code == AdmissionReviewErrorCodes.InvalidPolicy);
	}
	[Theory]
	[InlineData("waitlist")]
	[InlineData("reject")]
	public void Review_negative_route_must_depend_on_the_actual_recorded_decision(string decision)
	{
		var facts = new Dictionary<string, string> { ["review.decision"] = decision, ["application.status"] = "Submitted" };
		Should.Throw<BusinessException>(() => AdmissionReviewRouteRules.EnsureRoute(decision, Route(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), facts));
		Should.Throw<BusinessException>(() => AdmissionReviewRouteRules.EnsureRoute(decision, null, facts));
		var matching = "{\"field\":\"review.decision\",\"operator\":\"Equals\",\"value\":\"" + decision + "\"}";
		var generic = "{\"field\":\"application.status\",\"operator\":\"Equals\",\"value\":\"Submitted\"}";
		AdmissionReviewRouteRules.EnsureRoute(decision, Conditional(matching), facts);
		AdmissionReviewRouteRules.EnsureRoute(decision, Conditional("{\"operator\":\"ALL\",\"conditions\":[" + matching + "," + generic + "]}"), facts);
		Should.Throw<BusinessException>(() => AdmissionReviewRouteRules.EnsureRoute(decision, Conditional("{\"operator\":\"ANY\",\"conditions\":[" + matching + "," + generic + "]}"), facts));
		AdmissionReviewRouteRules.EnsureRoute("approve", null, facts);
	}
	private static AdmissionWorkflowStageDefinition Stage(Guid workflow, string code, AdmissionWorkflowStageType type, string? json, bool entry = false, bool terminal = false) =>
		new(Guid.NewGuid(), workflow, code, code, type, entry ? 0 : 1, true, false, null, json, entry, terminal);
	private static AdmissionWorkflowTransitionDefinition Route(Guid workflow, Guid from, Guid to) => new(Guid.NewGuid(), workflow, from, to, 0, isDefault: true);
	private static AdmissionWorkflowTransitionDefinition Conditional(string json) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, json);
}
