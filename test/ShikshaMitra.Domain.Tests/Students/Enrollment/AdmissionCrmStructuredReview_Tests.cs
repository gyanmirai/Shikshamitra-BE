using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmStructuredReview_Tests
{
	[Fact]
	public void Configuration_rejects_duplicate_criteria_and_invalid_review_count()
	{
		var config = CreateConfiguration();
		config.Version.ShouldBe(1);
		Should.Throw<BusinessException>(() => config.Update(true, "Committee", 0,
			true, Criteria(), Guid.NewGuid)).Code.ShouldBe("AdmissionCrm:InvalidRequiredReviewCount");
		Should.Throw<BusinessException>(() => config.Update(true, "Committee", 2, true,
			[
				new("readiness", "Readiness", 5, true),
				new("READINESS", "Duplicate", 5, true)
			], Guid.NewGuid)).Code.ShouldBe("AdmissionCrm:DuplicateReviewCriterionKey");
	}

	[Fact]
	public void Review_snapshots_configuration_and_prevents_duplicate_reviewer_assignment()
	{
		var now = DateTime.UtcNow;
		var review = CreateReview(now);
		var reviewer = Guid.NewGuid();
		review.Assign(Guid.NewGuid(), reviewer, now.AddDays(2), now);
		Should.Throw<BusinessException>(() =>
			review.Assign(Guid.NewGuid(), reviewer, now.AddDays(3), now))
			.Code.ShouldBe("AdmissionCrm:ReviewerAlreadyAssigned");
		review.Criteria.Count.ShouldBe(2);
		review.RequiredCompletedReviews.ShouldBe(2);
	}

	[Fact]
	public void Reviewer_must_declare_no_conflict_and_complete_required_rubric()
	{
		var now = DateTime.UtcNow;
		var review = CreateReview(now);
		var reviewer = Guid.NewGuid();
		var assignment = review.Assign(Guid.NewGuid(), reviewer, now.AddDays(2), now);

		Should.Throw<BusinessException>(() => review.Submit(assignment.Id, reviewer,
			AdmissionCrmReviewRecommendation.Admit, "Strong candidate.", Scores(4, 4), now))
			.Code.ShouldBe("AdmissionCrm:ReviewConflictDeclarationRequired");
		review.DeclareConflict(assignment.Id, reviewer, false, null, now);
		Should.Throw<BusinessException>(() => review.Submit(assignment.Id, reviewer,
			AdmissionCrmReviewRecommendation.Admit, "Strong candidate.",
			new Dictionary<string, int?> { ["readiness"] = 4 }, now))
			.Code.ShouldBe("AdmissionCrm:RequiredReviewScoreMissing");
	}

	[Fact]
	public void Conflict_recuses_reviewer_and_preserves_reason()
	{
		var now = DateTime.UtcNow;
		var review = CreateReview(now);
		var reviewer = Guid.NewGuid();
		var assignment = review.Assign(Guid.NewGuid(), reviewer, now.AddDays(2), now);
		review.DeclareConflict(assignment.Id, reviewer, true, "Applicant is a close relative.", now);
		assignment.Status.ShouldBe(AdmissionCrmReviewAssignmentStatus.Recused);
		assignment.ConflictReason.ShouldNotBeNull().ShouldContain("close relative");
		Should.Throw<BusinessException>(() => review.Submit(assignment.Id, reviewer,
			AdmissionCrmReviewRecommendation.Admit, "Should not submit.", Scores(4, 4), now));
	}

	[Fact]
	public void Required_completed_reviews_make_case_ready_and_closeable()
	{
		var now = DateTime.UtcNow;
		var review = CreateReview(now);
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		var firstAssignment = review.Assign(Guid.NewGuid(), first, now.AddDays(2), now);
		var secondAssignment = review.Assign(Guid.NewGuid(), second, now.AddDays(2), now);
		review.DeclareConflict(firstAssignment.Id, first, false, null, now);
		review.DeclareConflict(secondAssignment.Id, second, false, null, now);
		review.Submit(firstAssignment.Id, first, AdmissionCrmReviewRecommendation.Admit,
			"Ready to admit.", Scores(4, 5), now.AddHours(1));
		review.Status.ShouldBe(AdmissionCrmStructuredReviewStatus.InReview);
		review.Submit(secondAssignment.Id, second, AdmissionCrmReviewRecommendation.Waitlist,
			"Waitlist until documents arrive.", Scores(3, 4), now.AddHours(2));
		review.Status.ShouldBe(AdmissionCrmStructuredReviewStatus.ReadyForDecision);
		review.Close(Guid.NewGuid(), now.AddHours(3), "Committee evidence considered in final decision.");
		review.Status.ShouldBe(AdmissionCrmStructuredReviewStatus.Closed);
	}

	private static AdmissionCrmReviewConfiguration CreateConfiguration()
	{
		var configuration = new AdmissionCrmReviewConfiguration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		configuration.Update(true, "Admissions committee", 2, true, Criteria(), Guid.NewGuid);
		return configuration;
	}

	private static AdmissionCrmStructuredReview CreateReview(DateTime now)
	{
		var config = CreateConfiguration();
		return new AdmissionCrmStructuredReview(Guid.NewGuid(), config.TenantId, config.AcademicYearId,
			Guid.NewGuid(), config, now, Guid.NewGuid);
	}

	private static IReadOnlyCollection<AdmissionCrmReviewCriterionInput> Criteria() =>
	[
		new("readiness", "School readiness", 5, true),
		new("family_context", "Family context", 5, true)
	];

	private static IReadOnlyDictionary<string, int?> Scores(int readiness, int familyContext) =>
		new Dictionary<string, int?>
		{
			["readiness"] = readiness,
			["family_context"] = familyContext
		};
}
