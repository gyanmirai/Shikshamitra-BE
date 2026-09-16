using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamScheduleStatusResolver_Tests
{
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
