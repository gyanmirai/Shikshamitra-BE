using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmSlaClock_Tests
{
	private static AdmissionCrmSlaPolicyDto CreatePolicy(params DateTime[] holidays) => new()
	{
		TimeZoneId = "Asia/Kolkata",
		WorkingDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
			DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
		WorkingDayStartHour = 8,
		WorkingDayEndHour = 17,
		Holidays = holidays
	};

	[Fact]
	public void Calculates_only_the_local_working_window()
	{
		var from = new DateTimeOffset(2026, 7, 13, 2, 0, 0, TimeSpan.Zero);
		var to = new DateTimeOffset(2026, 7, 13, 13, 0, 0, TimeSpan.Zero);

		AdmissionCrmSlaClock.CalculateWorkingHours(from, to, CreatePolicy()).ShouldBe(9d);
	}

	[Fact]
	public void Skips_sundays_and_configured_holidays()
	{
		var from = new DateTimeOffset(2026, 7, 11, 4, 30, 0, TimeSpan.Zero);
		var to = new DateTimeOffset(2026, 7, 14, 4, 30, 0, TimeSpan.Zero);

		AdmissionCrmSlaClock.CalculateWorkingHours(
			from, to, CreatePolicy(new DateTime(2026, 7, 13))).ShouldBe(9d);
	}

	[Fact]
	public void Rejects_an_invalid_working_calendar()
	{
		var policy = CreatePolicy();
		policy.WorkingDayStartHour = 17;
		policy.WorkingDayEndHour = 8;

		Should.Throw<ArgumentException>(() => AdmissionCrmSlaClock.CalculateWorkingHours(
			DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, policy));
	}
}
