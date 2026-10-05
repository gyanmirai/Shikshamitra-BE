using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class RubricScoring_Tests
{
	[Fact]
	public void Performance_Criteria_Use_Highest_Level_Not_Row_Order_Or_Stored_Points()
	{
		var rubric = CreateRubric(RubricScoringModel.PerformanceLevels,
			"""[{"Score":2},{"Score":5},{"Score":1}]""");
		var required = Criterion(rubric.Id, null);
		var optional = Criterion(rubric.Id, 2, required: false);
		rubric.ReplaceCriteria([required, optional]);
		rubric.GetCriterionMaximum(required).ShouldBe(5);
		rubric.GetCriterionMaximum(optional).ShouldBe(5);
		rubric.TotalMarks.ShouldBe(10);
		rubric.Publish();
		rubric.SetCriteriaForRead([required, optional]);
		rubric.TotalMarks.ShouldBe(10);
	}

	[Theory]
	[InlineData(RubricScoringModel.PointsBased, 10, null, 20)]
	[InlineData(RubricScoringModel.WeightedPercentage, null, 50, 100)]
	[InlineData(RubricScoringModel.Checklist, 1, null, 2)]
	[InlineData(RubricScoringModel.RatingScale, 4, null, 8)]
	public void Other_Models_Preserve_Criterion_Values(RubricScoringModel model, int? points, int? weight, int total)
	{
		var rubric = CreateRubric(model, null);
		rubric.ReplaceCriteria([Criterion(rubric.Id, points, weight), Criterion(rubric.Id, points, weight)]);
		rubric.TotalMarks.ShouldBe(total);
	}

	private static AssessmentRubric CreateRubric(RubricScoringModel model, string? levels) =>
		new(Guid.NewGuid(), null, null, null, null, null, null, null, null, null,
			true, WorkType.Both, model, RubricVisibility.Institution, "Rubric", null, null, null, null, levels);

	private static AssessmentRubricCriterion Criterion(Guid rubricId, decimal? points, decimal? weight = null, bool required = true) =>
		new(Guid.NewGuid(), null, rubricId, "Accuracy", null, null, null, required, points, 1, weight);
}
