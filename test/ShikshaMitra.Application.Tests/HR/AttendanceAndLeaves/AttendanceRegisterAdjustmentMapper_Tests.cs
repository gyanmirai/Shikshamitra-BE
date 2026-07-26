using System;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceRegisterAdjustmentMapper_Tests
{
	[Theory]
	[InlineData("07:00", 7, 0)]
	[InlineData("19:30:45", 19, 30)]
	public void Plain_Time_Should_Use_A_Stable_Zero_Offset(string value, int expectedHour, int expectedMinute)
	{
		var parsed = AttendanceRegisterAdjustmentMapper.ParseOptionalTime(value);

		parsed.ShouldNotBeNull();
		parsed.Value.Offset.ShouldBe(TimeSpan.Zero);
		parsed.Value.Hour.ShouldBe(expectedHour);
		parsed.Value.Minute.ShouldBe(expectedMinute);
	}

	[Fact]
	public void Explicit_Offset_Should_Be_Preserved()
	{
		var parsed = AttendanceRegisterAdjustmentMapper.ParseOptionalTime("2026-07-26T07:00:00+05:30");

		parsed.ShouldNotBeNull();
		parsed.Value.Offset.ShouldBe(TimeSpan.FromHours(5.5));
		parsed.Value.Hour.ShouldBe(7);
	}

	[Fact]
	public void Applying_Time_Should_Use_The_Attendance_Date_And_Parsed_Offset()
	{
		var attendanceDate = new DateOnly(2027, 3, 1);
		var parsed = AttendanceRegisterAdjustmentMapper.ParseOptionalTime("19:00");

		var applied = AttendanceRegisterAdjustmentMapper.ApplyTimeToDate(attendanceDate, parsed);

		applied.ShouldBe(new DateTimeOffset(2027, 3, 1, 19, 0, 0, TimeSpan.Zero));
	}
}
