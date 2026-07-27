using System;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentAlignment_Tests
{
	[Fact]
	public void AssignmentType_Trims_Name_And_Forces_NoEvaluation()
	{
		var assignmentType = CreateAssignmentType(
			"  Project  ",
			evaluationRequired: false,
			evaluationMethod: EvaluationMethod.Marks,
			marks: 25);

		Assert.Equal("Project", assignmentType.Name);
		Assert.False(assignmentType.EvaluationRequired);
		Assert.Equal(EvaluationMethod.NoEvaluation, assignmentType.DefaultEvaluationMethod);
		Assert.Null(assignmentType.DefaultMarks);
	}

	[Fact]
	public void AssignmentType_Requires_Evaluation_Method_When_Enabled()
	{
		var exception = Assert.Throws<BusinessException>(() =>
			CreateAssignmentType("Project", evaluationRequired: true));

		Assert.Equal(HomeworkAssignmentErrorCodes.EvaluationMethodRequired, exception.Code);
	}

	[Fact]
	public void AssignmentType_Seeder_Compatible_Constructor_Values_Should_Be_Valid()
	{
		var assignmentType = new AssignmentType(
			Guid.NewGuid(),
			"Project",
			Guid.NewGuid(),
			Guid.NewGuid(),
			defaultMarks: 100,
			evaluationRequired: true,
			defaultEvaluationMethod: EvaluationMethod.Marks);

		Assert.True(assignmentType.EvaluationRequired);
		Assert.Equal(EvaluationMethod.Marks, assignmentType.DefaultEvaluationMethod);
		Assert.Equal(100, assignmentType.DefaultMarks);
	}

	[Fact]
	public void AssignmentType_Rejects_Zero_Due_Days()
	{
		var exception = Assert.Throws<BusinessException>(() =>
			CreateAssignmentType(
				"Project",
				evaluationRequired: true,
				evaluationMethod: EvaluationMethod.FeedbackOnly,
				dueDays: 0));

		Assert.Equal(HomeworkAssignmentErrorCodes.InvalidDefaultDueDays, exception.Code);
	}

	[Fact]
	public void Effective_Evaluation_Should_Fallback_Safely_For_Legacy_Null_Method()
	{
		var assignmentWithMarks = CreateAssignment(totalMarks: 25);
		var assignmentWithoutMarks = CreateAssignment(totalMarks: 0);

		Assert.Equal(EvaluationMethod.Marks, AssignmentEvaluationResolver.Resolve(assignmentWithMarks).Method);
		Assert.Equal(EvaluationMethod.FeedbackOnly, AssignmentEvaluationResolver.Resolve(assignmentWithoutMarks).Method);
	}

	[Fact]
	public void Effective_Evaluation_Should_Reject_Incompatible_Marks()
	{
		var assignment = CreateAssignment(
			totalMarks: 25,
			evaluationRequired: true,
			evaluationMethod: EvaluationMethod.FeedbackOnly);

		var exception = Assert.Throws<BusinessException>(() => AssignmentEvaluationResolver.Resolve(assignment));

		Assert.Equal(HomeworkAssignmentErrorCodes.IncompatibleEvaluationMarks, exception.Code);
	}

	[Theory]
	[InlineData(EvaluationMethod.Grade)]
	[InlineData(EvaluationMethod.Checklist)]
	public void Effective_Evaluation_Should_Reject_Unsupported_Persistence(EvaluationMethod method)
	{
		var assignment = CreateAssignment(0, true, method);

		var exception = Assert.Throws<BusinessException>(() => AssignmentEvaluationResolver.Resolve(assignment));

		Assert.Equal(HomeworkAssignmentErrorCodes.UnsupportedEvaluationPersistence, exception.Code);
	}

	[Fact]
	public void Assignment_PublishDueSchedules_Should_Only_Persist_Due_Schedules()
	{
		var now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
		var assignment = CreateAssignment(25);
		var dueSchedule = new AssignmentDivisionSchedule(
			Guid.NewGuid(), assignment.Id, Guid.NewGuid(), now.AddMinutes(-1), now.AddDays(1));
		var futureSchedule = new AssignmentDivisionSchedule(
			Guid.NewGuid(), assignment.Id, Guid.NewGuid(), now.AddMinutes(1), now.AddDays(1));
		assignment.AddDivisionSchedule(dueSchedule);
		assignment.AddDivisionSchedule(futureSchedule);

		assignment.PublishDueSchedules(now);
		assignment.PublishDueSchedules(now.AddSeconds(1));

		Assert.True(dueSchedule.IsPublished);
		Assert.Equal(now, dueSchedule.PublishedAt);
		Assert.False(futureSchedule.IsPublished);
		Assert.Null(futureSchedule.PublishedAt);
	}

	[Fact]
	public void DivisionSchedule_Publish_Preserves_Planned_Time_And_Is_Idempotent()
	{
		var planned = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc);
		var firstPublishedAt = planned.AddMinutes(5);
		var schedule = new AssignmentDivisionSchedule(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			planned,
			planned.AddDays(3));

		schedule.Publish(firstPublishedAt);
		schedule.Publish(firstPublishedAt.AddHours(1));

		Assert.True(schedule.IsPublished);
		Assert.Equal(planned, schedule.PublishDate);
		Assert.Equal(firstPublishedAt, schedule.PublishedAt);
	}

	private static AssignmentEntity CreateAssignment(
		decimal totalMarks,
		bool? evaluationRequired = null,
		EvaluationMethod? evaluationMethod = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Legacy assignment",
			"Legacy assignment",
			Guid.NewGuid(),
			SubmissionType.InPersonSubmission,
			totalMarks,
			evaluationRequired: evaluationRequired,
			evaluationMethod: evaluationMethod);

	private static AssignmentType CreateAssignmentType(
		string name,
		bool evaluationRequired,
		EvaluationMethod? evaluationMethod = null,
		decimal? marks = null,
		int? dueDays = null) =>
		new(
			Guid.NewGuid(),
			name,
			Guid.NewGuid(),
			Guid.NewGuid(),
			defaultDueDays: dueDays,
			defaultMarks: marks,
			evaluationRequired: evaluationRequired,
			defaultEvaluationMethod: evaluationMethod);
}
