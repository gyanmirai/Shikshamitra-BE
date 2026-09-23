using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanRevision_Tests
{
	[Theory]
	[InlineData(LessonPlanStatus.Approved)]
	[InlineData(LessonPlanStatus.Rejected)]
	public void Terminal_Plan_Should_Reject_Edit_And_Resubmission(LessonPlanStatus status)
	{
		var chapterId = Guid.NewGuid();
		var plan = CreatePlan(chapterId);
		plan.Submit();
		if (status == LessonPlanStatus.Approved)
		{
			plan.Approve(Guid.NewGuid(), "Approved.");
		}
		else
		{
			plan.Reject(Guid.NewGuid(), "Create a replacement plan.");
		}

		var updateError = Should.Throw<LessonPlanNotEditableException>(() =>
			plan.UpdatePlan(chapterId, "Updated topic", null, null, null, 60));
		var submitError = Should.Throw<LessonPlanNotEditableException>(() => plan.Submit());

		updateError.Code.ShouldBe(CurriculumErrorCodes.LessonPlanNotEditable);
		submitError.Code.ShouldBe(CurriculumErrorCodes.LessonPlanNotEditable);
		plan.Status.ShouldBe(status);
	}

	[Fact]
	public void NeedsChanges_Plan_Should_Require_A_Meaningful_Edit_Before_Resubmission()
	{
		var chapterId = Guid.NewGuid();
		var plan = CreatePlan(chapterId);
		plan.UpdatePlan(chapterId, "Fractions", "Compare fractions", "Discussion", "Fraction cards", 50);
		plan.Submit();
		plan.RequestChanges(Guid.NewGuid(), "Add a practical example.");

		Should.Throw<LessonPlanRevisionRequiredException>(() => plan.Submit());
		Should.Throw<LessonPlanRevisionRequiredException>(() =>
			plan.UpdatePlan(chapterId, " Fractions ", "Compare fractions", "Discussion", "Fraction cards", 50));
		plan.Status.ShouldBe(LessonPlanStatus.NeedsChanges);

		plan.UpdatePlan(chapterId, "Fractions with Pizza", "Compare fractions", "Discussion", "Fraction cards", 50);
		plan.Status.ShouldBe(LessonPlanStatus.Draft);

		plan.Submit();
		plan.Status.ShouldBe(LessonPlanStatus.Submitted);
	}

	private static LessonPlan CreatePlan(Guid chapterId) => new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			chapterId,
			DateTime.UtcNow,
			"Fractions",
			50);
}
