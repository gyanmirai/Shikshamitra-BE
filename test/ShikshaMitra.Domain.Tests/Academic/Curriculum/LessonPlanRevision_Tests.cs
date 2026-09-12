using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanRevision_Tests
{
	[Fact]
	public void NeedsChanges_Plan_Should_Require_A_Meaningful_Edit_Before_Resubmission()
	{
		var chapterId = Guid.NewGuid();
		var plan = new LessonPlan(
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
}
