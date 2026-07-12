using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmSpecialRoute_Tests
{
	[Fact]
	public void Route_definition_requires_distinct_publisher()
	{
		var maker = Guid.NewGuid(); var definition = CreateDefinition(); definition.Submit(maker);
		Should.Throw<BusinessException>(() => definition.Publish(maker)).Code.ShouldBe("AdmissionCrm:DistinctRouteApproverRequired");
		definition.Publish(Guid.NewGuid()); definition.Status.ShouldBe(AdmissionCrmRouteDefinitionStatus.Published);
	}

	[Fact]
	public void Route_change_supersedes_prior_assignment_without_erasing_history()
	{
		var prior = CreateAssignment(null); prior.Supersede();
		var next = CreateAssignment(prior.Id);
		prior.Status.ShouldBe(AdmissionCrmRouteEligibilityStatus.Superseded); prior.IsPrimary.ShouldBeFalse();
		next.PreviousAssignmentId.ShouldBe(prior.Id); next.IsPrimary.ShouldBeTrue();
	}

	[Fact]
	public void Route_eligibility_decision_is_audited()
	{
		var assignment = CreateAssignment(null); var reviewer = Guid.NewGuid();
		assignment.Evaluate(AdmissionCrmRouteEligibilityStatus.Eligible, "Verified sibling and policy evidence.", reviewer, DateTime.UtcNow);
		assignment.Status.ShouldBe(AdmissionCrmRouteEligibilityStatus.Eligible);
		assignment.EvaluatedByUserId.ShouldBe(reviewer); assignment.EvaluationReason.ShouldContain("Verified sibling");
	}

	[Fact]
	public void Route_appeal_preserves_original_decision_and_requires_distinct_approver()
	{
		var requester = Guid.NewGuid();
		var appeal = new AdmissionCrmRouteAppeal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), AdmissionCrmRouteEligibilityStatus.Ineligible, AdmissionCrmRouteEligibilityStatus.Eligible,
			"New authority evidence is available.", "[\"AUTH-100\"]", requester, DateTime.UtcNow);
		Should.Throw<BusinessException>(() => appeal.Decide(true, "Self approval.", requester, DateTime.UtcNow)).Code
			.ShouldBe("AdmissionCrm:DistinctRouteAppealApproverRequired");
		appeal.Decide(true, "Evidence changes the governed eligibility outcome.", Guid.NewGuid(), DateTime.UtcNow);
		appeal.Status.ShouldBe(AdmissionCrmRouteAppealStatus.Approved);
		appeal.OriginalStatus.ShouldBe(AdmissionCrmRouteEligibilityStatus.Ineligible);
	}

	private static AdmissionCrmRouteDefinition CreateDefinition() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"SIBLING", "Sibling priority", AdmissionCrmRouteType.SiblingPriority, 1, "{\"requiresPolicyEvaluation\":true}",
		DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddYears(1));

	private static AdmissionCrmRouteAssignment CreateAssignment(Guid? previousId) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), 1, AdmissionCrmRouteType.SiblingPriority,
		"{\"relatedStudentId\":\"00000000-0000-0000-0000-000000000001\"}", "[\"SIB-100\"]", previousId,
		previousId.HasValue ? "Route changed after evidence review." : "Initial route assignment.", Guid.NewGuid(), DateTime.UtcNow);
}
