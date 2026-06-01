using System;
using System.Collections.Generic;
using ShikshaMitra.HR;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceManagement_Tests
{
	[Fact]
	public void Publish_Template_Should_Require_100_Percent_Section_Weight()
	{
		var template = new PerformanceTemplate(Guid.NewGuid(), Guid.NewGuid(), "Teacher", PerformanceStaffCategory.Teacher, null);

		var ex = Should.Throw<BusinessException>(() => template.Publish(90));

		ex.Code.ShouldBe(HRErrorCodes.PerformanceTemplateWeightsInvalid);
	}

	[Fact]
	public void Publish_Template_Should_Require_A_Valid_Rubric()
	{
		var template = new PerformanceTemplate(Guid.NewGuid(), Guid.NewGuid(), "Teacher", PerformanceStaffCategory.Teacher, null);

		var ex = Should.Throw<BusinessException>(() => template.Publish(100, hasValidRubric: false));

		ex.Code.ShouldBe(HRErrorCodes.PerformanceTemplateRubricInvalid);
	}

	[Fact]
	public void Draft_Cycle_Should_Not_Allow_Assignment_Or_Calibration()
	{
		var cycle = new ProfessionalReviewCycle(
			Guid.NewGuid(),
			Guid.NewGuid(),
			null,
			"Annual",
			PerformanceCycleType.Annual,
			DateTime.UtcNow.Date,
			DateTime.UtcNow.Date.AddMonths(6),
			null,
			null,
			null,
			5,
			true,
			null,
			null,
			ProfessionalReviewCycleStatus.Draft);

		Should.Throw<BusinessException>(() => cycle.EnsureCanAssign())
			.Code.ShouldBe(HRErrorCodes.PerformanceReviewCycleInvalidStatus);
		Should.Throw<BusinessException>(() => cycle.MoveToCalibration())
			.Code.ShouldBe(HRErrorCodes.PerformanceReviewCycleInvalidStatus);
	}

	[Fact]
	public void Cycle_Should_Finalize_Only_From_Calibration()
	{
		var cycle = new ProfessionalReviewCycle(
			Guid.NewGuid(),
			Guid.NewGuid(),
			null,
			"Annual",
			PerformanceCycleType.Annual,
			DateTime.UtcNow.Date,
			DateTime.UtcNow.Date.AddMonths(6),
			null,
			null,
			null,
			5,
			true,
			null,
			null,
			ProfessionalReviewCycleStatus.Published);

		Should.Throw<BusinessException>(() => cycle.FinalizeCycle())
			.Code.ShouldBe(HRErrorCodes.PerformanceReviewCycleInvalidStatus);

		cycle.MoveToCalibration();
		cycle.FinalizeCycle();

		cycle.Status.ShouldBe(ProfessionalReviewCycleStatus.Finalized);
	}

	[Fact]
	public void Stage_Should_Lock_After_Submit()
	{
		var stage = new PerformanceReviewStage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PerformanceReviewStageType.SelfAppraisal);

		stage.Submit("Done", DateTime.UtcNow);

		Should.Throw<BusinessException>(() => stage.SaveDraft("Change")).Code.ShouldBe(HRErrorCodes.PerformanceReviewStageLocked);
	}

	[Fact]
	public void Finalized_Review_Should_Be_Readonly_Until_Reopened()
	{
		var assignment = new PerformanceReviewAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

		assignment.FinalizeReview(82.5m, "Very Good", DateTime.UtcNow);

		Should.Throw<BusinessException>(() => assignment.EnsureEditable()).Code.ShouldBe(HRErrorCodes.PerformanceReviewFinalized);
		assignment.Reopen();
		Should.NotThrow(() => assignment.EnsureEditable());
	}

	[Fact]
	public void Published_Template_Can_Be_Archived_But_Not_Edited()
	{
		var template = new PerformanceTemplate(Guid.NewGuid(), Guid.NewGuid(), "Teacher", PerformanceStaffCategory.Teacher, null);

		template.Publish(100);

		Should.Throw<BusinessException>(() => template.Update("Changed", PerformanceStaffCategory.Teacher, null, 5, true, null))
			.Code.ShouldBe(HRErrorCodes.PerformanceTemplatePublished);
		template.Archive();
		template.Status.ShouldBe(PerformanceTemplateStatus.Archived);
	}

	[Fact]
	public void Score_Should_Exclude_Not_Applicable_Responses()
	{
		var templateId = Guid.NewGuid();
		var sectionId = Guid.NewGuid();
		var reviewId = Guid.NewGuid();
		var stageId = Guid.NewGuid();
		var c1 = Guid.NewGuid();
		var c2 = Guid.NewGuid();

		var score = PerformanceScoreCalculator.CalculateFinalScore(
			new List<PerformanceTemplateSection> { new(sectionId, Guid.NewGuid(), templateId, "Academic", 100, 1) },
			new List<PerformanceTemplateCriterion>
			{
				new(c1, Guid.NewGuid(), templateId, sectionId, "Delivery", null, 50, false, 1),
				new(c2, Guid.NewGuid(), templateId, sectionId, "Results", null, 50, false, 2)
			},
			new List<PerformanceReviewResponse>
			{
				new(Guid.NewGuid(), Guid.NewGuid(), reviewId, stageId, c1, 5, false, null),
				new(Guid.NewGuid(), Guid.NewGuid(), reviewId, stageId, c2, null, true, null)
			},
			5);

		score.ShouldBe(100);
	}
}
