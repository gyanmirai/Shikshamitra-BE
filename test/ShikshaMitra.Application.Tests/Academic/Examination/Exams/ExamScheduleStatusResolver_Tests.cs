using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamScheduleStatusResolver_Tests
{
	[Theory]
	[InlineData(8, 39, 59, 999, ExamStatus.Published)]
	[InlineData(8, 40, 0, 0, ExamStatus.Ongoing)]
	[InlineData(9, 30, 0, 0, ExamStatus.Ongoing)]
	[InlineData(9, 30, 0, 1, ExamStatus.Completed)]
	public void Reported_exam_uses_exact_school_clock_boundaries(int hour, int minute, int second, int millisecond, ExamStatus expected)
	{
		var window = ExamScheduleStatusResolver.GetWindow([
			(new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
			 new DateTime(2026, 10, 30, 14, 10, 0, DateTimeKind.Utc),
			 new DateTime(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc))]);
		ExamScheduleStatusResolver.Resolve(ExamStatus.Published, window.StartAt, window.EndAt,
			new DateTime(2026, 10, 30, hour, minute, second, millisecond, DateTimeKind.Utc)).ShouldBe(expected);
	}

	[Fact]
	public void Window_combines_dates_with_clock_components_and_keeps_multi_day_gaps_ongoing()
	{
		var window = ExamScheduleStatusResolver.GetWindow([
			(new DateTime(2026, 10, 30), new DateTime(2000, 1, 1, 14, 10, 0), new DateTime(2000, 1, 1, 15, 0, 0)),
			(new DateTime(2026, 11, 2), new DateTime(2000, 1, 1, 9, 0, 0), new DateTime(2000, 1, 1, 10, 0, 0))]);
		window.StartAt.ShouldBe(new DateTime(2026, 10, 30, 14, 10, 0));
		window.EndAt.ShouldBe(new DateTime(2026, 11, 2, 10, 0, 0));
		ExamScheduleStatusResolver.Resolve(ExamStatus.Published, window.StartAt, window.EndAt,
			new DateTime(2026, 10, 31, 18, 30, 0, DateTimeKind.Utc)).ShouldBe(ExamStatus.Ongoing);
	}

	[Fact]
	public void Window_preserves_missing_clock_defaults_without_inventing_dates()
	{
		var missing = ExamScheduleStatusResolver.GetWindow([(null, null, null)]);
		missing.StartAt.ShouldBeNull(); missing.EndAt.ShouldBeNull();
		ExamScheduleStatusResolver.Resolve(ExamStatus.Published, missing.StartAt, missing.EndAt, DateTime.UtcNow).ShouldBe(ExamStatus.Pending);
		var window = ExamScheduleStatusResolver.GetWindow([(new DateTime(2026, 10, 30), null, null)]);
		window.StartAt.ShouldBe(new DateTime(2026, 10, 30));
		window.EndAt.ShouldBe(new DateTime(2026, 10, 31).AddTicks(-1));
		ExamScheduleStatusResolver.Resolve(ExamStatus.Pending, window.StartAt, window.EndAt,
			new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc)).ShouldBe(ExamStatus.Pending);
	}

	[Fact]
	public void Resolve_Should_Keep_Persisted_Completed_Status_Before_A_Future_Schedule()
	{
		ExamScheduleStatusResolver.Resolve(ExamStatus.Completed,
			new DateTime(2026, 9, 17, 9, 0, 0), new DateTime(2026, 9, 17, 10, 0, 0),
			new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc)).ShouldBe(ExamStatus.Completed);
	}

	[Fact]
	public void Resolve_Should_Use_Institution_Time_When_The_Server_Clock_Is_Utc()
	{
		var scheduledStart = new DateTime(2026, 9, 15, 10, 15, 0, DateTimeKind.Unspecified);
		var scheduledEnd = new DateTime(2026, 9, 15, 10, 50, 0, DateTimeKind.Unspecified);
		var currentUtc = new DateTime(2026, 9, 15, 5, 6, 0, DateTimeKind.Utc);

		var status = ExamScheduleStatusResolver.Resolve(
			ExamStatus.Published,
			scheduledStart,
			scheduledEnd,
			currentUtc);

		status.ShouldBe(ExamStatus.Ongoing);
	}
}
