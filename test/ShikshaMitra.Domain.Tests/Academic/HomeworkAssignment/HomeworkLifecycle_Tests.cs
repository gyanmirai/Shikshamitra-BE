using System;
using Xunit;
using Volo.Abp;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkLifecycle_Tests
{
	[Fact]
	public void New_homework_starts_as_a_draft_with_unpublished_division_schedules()
	{
		var homework = CreateHomework();
		var schedule = new HomeworkDivisionSchedule(
			Guid.NewGuid(),
			homework.Id,
			Guid.NewGuid(),
			DateTime.UtcNow,
			DateTime.UtcNow.AddDays(1));

		homework.AddDivisionSchedule(schedule);

		Assert.Equal(HomeworkStatus.Draft, homework.Status);
		Assert.False(homework.IsPublished);
		Assert.False(schedule.IsPublished);
	}

	[Fact]
	public void Publishing_homework_transitions_status_and_publishes_all_divisions()
	{
		var homework = CreateHomework();
		homework.AddDivisionSchedule(CreateSchedule(homework));
		homework.AddDivisionSchedule(CreateSchedule(homework));

		homework.ChangeStatus(HomeworkStatus.Published);
		homework.PublishAll();

		Assert.Equal(HomeworkStatus.Published, homework.Status);
		Assert.All(homework.DivisionSchedules, schedule => Assert.True(schedule.IsPublished));
	}

	[Fact]
	public void Published_homework_cannot_be_published_again()
	{
		var homework = CreateHomework();
		homework.ChangeStatus(HomeworkStatus.Published);

		Assert.Throws<BusinessException>(() => homework.ChangeStatus(HomeworkStatus.Published));
	}

	private static HomeworkAssignmentEntity CreateHomework() =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Homework title",
			"Homework description",
			Guid.NewGuid(),
			SubmissionType.InPersonSubmission);

	private static HomeworkDivisionSchedule CreateSchedule(HomeworkAssignmentEntity homework) =>
		new(
			Guid.NewGuid(),
			homework.Id,
			Guid.NewGuid(),
			DateTime.UtcNow,
			DateTime.UtcNow.AddDays(1));
}
