using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCycle_Tests
{
	[Fact]
	public void New_cycle_starts_as_draft_and_normalizes_code()
	{
		var cycle = new AdmissionCycle(
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Admissions 2027-28",
			" adm-2027 ",
			Guid.NewGuid(),
			new DateTime(2026, 10, 1),
			new DateTime(2027, 3, 31));

		cycle.Status.ShouldBe(AdmissionCycleStatus.Draft);
		cycle.Code.ShouldBe("ADM-2027");
	}

	[Fact]
	public void Date_range_must_be_positive()
	{
		Should.Throw<BusinessException>(() => new AdmissionCycle(
			Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2027, 3, 31), new DateTime(2027, 3, 1)))
			.Code.ShouldBe(AdmissionErrorCodes.CycleDateRangeInvalid);
	}

	[Fact]
	public void Published_cycle_is_immutable_and_can_close()
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2026, 10, 1), new DateTime(2027, 3, 31));

		cycle.Publish(Guid.NewGuid(), new DateTime(2026, 9, 1));
		cycle.Status.ShouldBe(AdmissionCycleStatus.Published);
		cycle.Close();
		cycle.Status.ShouldBe(AdmissionCycleStatus.Closed);
		Should.Throw<BusinessException>(() => cycle.UpdateDetails("Changed", "NEW", cycle.AcademicYearId,
			cycle.StartDate, cycle.EndDate)).Code.ShouldBe(AdmissionErrorCodes.CycleCannotBeModified);
	}

	[Fact]
	public void Closed_cycle_can_be_reopened_and_archived_through_controlled_transitions()
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2027, 1, 1), new DateTime(2027, 4, 30));
		var actor = Guid.NewGuid();

		cycle.Publish(actor, new DateTime(2026, 10, 1));
		cycle.Close(actor, new DateTime(2027, 2, 1));
		cycle.Reopen(actor, new DateTime(2027, 2, 2));

		cycle.Status.ShouldBe(AdmissionCycleStatus.Published);
		cycle.ReopenedBy.ShouldBe(actor);
		cycle.ClosedAt.ShouldBeNull();

		cycle.Close(actor, new DateTime(2027, 3, 1));
		cycle.Archive(actor, new DateTime(2027, 5, 1));
		cycle.Status.ShouldBe(AdmissionCycleStatus.Archived);
		cycle.ArchivedBy.ShouldBe(actor);
	}

	[Fact]
	public void Published_cycle_window_can_be_revised_but_draft_cannot_use_revision_path()
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2027, 1, 1), new DateTime(2027, 4, 30));

		Should.Throw<BusinessException>(() => cycle.ReviseWindow(
			new DateTime(2027, 1, 15), new DateTime(2027, 5, 15)));

		cycle.Publish(Guid.NewGuid(), new DateTime(2026, 10, 1));
		cycle.ReviseWindow(new DateTime(2027, 1, 15), new DateTime(2027, 5, 15));
		cycle.StartDate.ShouldBe(new DateTime(2027, 1, 15));
		cycle.EndDate.ShouldBe(new DateTime(2027, 5, 15));
	}

	[Fact]
	public void Scheduled_actions_must_be_ordered_and_are_cleared_after_execution()
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "Admissions", "ADM", Guid.NewGuid(),
			new DateTime(2027, 1, 1), new DateTime(2027, 4, 30));
		var publishAt = new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc);
		var closeAt = new DateTime(2027, 4, 30, 18, 0, 0, DateTimeKind.Utc);

		cycle.SetSchedule(publishAt, closeAt);
		cycle.Publish(Guid.NewGuid(), publishAt);

		cycle.ScheduledPublishAt.ShouldBeNull();
		cycle.ScheduledCloseAt.ShouldBe(closeAt);
		Should.Throw<BusinessException>(() => cycle.SetSchedule(closeAt, null))
			.Code.ShouldBe(AdmissionErrorCodes.CycleScheduleInvalid);
	}

	[Theory]
	[InlineData(AdmissionCycleStatus.Draft, "2027-01-01", AdmissionCycleWindowStatus.Upcoming)]
	[InlineData(AdmissionCycleStatus.Published, "2027-02-01", AdmissionCycleWindowStatus.Open)]
	[InlineData(AdmissionCycleStatus.Published, "2027-05-01", AdmissionCycleWindowStatus.Ended)]
	[InlineData(AdmissionCycleStatus.Closed, "2027-02-01", AdmissionCycleWindowStatus.Closed)]
	[InlineData(AdmissionCycleStatus.Archived, "2027-02-01", AdmissionCycleWindowStatus.Archived)]
	public void Operational_window_status_is_independent_from_lifecycle(
		AdmissionCycleStatus status, string today, AdmissionCycleWindowStatus expected)
	{
		AdmissionCycleWindowEvaluator.EvaluateForDate(status,
			new DateTime(2027, 2, 1), new DateTime(2027, 4, 30), DateTime.Parse(today))
			.ShouldBe(expected);
	}

	[Fact]
	public void Institution_date_uses_india_timezone_at_utc_boundary()
	{
		AdmissionCycleWindowEvaluator.GetInstitutionDate(
			new DateTime(2027, 1, 31, 20, 0, 0, DateTimeKind.Utc))
			.ShouldBe(new DateTime(2027, 2, 1));
	}
}
