using System;
using System.Text.Json;
using ShikshaMitra.Academic.HomeworkAssignment;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment.Tests;

public class LearningTaskScheduleTimestampTests
{
	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Unspecified)]
	[InlineData(DateTimeKind.Local)]
	public void Stored_utc_clock_is_serialized_with_its_real_offset(DateTimeKind kind)
	{
		var stored = new DateTime(2026, 10, 30, 11, 30, 0, kind);
		var response = new HomeworkDivisionScheduleDto
		{
			PublishAt = LearningTaskScheduleTimestamp.FromUtcStorage(stored),
			DueAt = LearningTaskScheduleTimestamp.FromUtcStorage(stored)
		};
		Assert.Equal(TimeSpan.Zero, response.DueAt.Offset);
		Assert.Equal(17, response.DueAt.ToOffset(TimeSpan.FromMinutes(330)).Hour);
		var roundTrip = JsonSerializer.Deserialize<HomeworkDivisionScheduleDto>(JsonSerializer.Serialize(response))!;
		Assert.Equal(response.DueAt, roundTrip.DueAt);
		Assert.Equal(TimeSpan.Zero, roundTrip.DueAt.Offset);
	}

	[Fact]
	public void Missing_publish_time_stays_missing()
	{
		Assert.Null(LearningTaskScheduleTimestamp.FromUtcStorage((DateTime?)null));
	}
}
