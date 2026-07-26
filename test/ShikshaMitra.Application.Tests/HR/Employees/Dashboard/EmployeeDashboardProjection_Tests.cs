using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees.Dashboard;

public class EmployeeDashboardProjection_Tests
{
	[Fact]
	public void Attendance_Percentage_Should_Use_Recorded_Full_And_Half_Day_Units()
	{
		var result = EmployeeDashboardProjection.CalculateAttendancePercentage(
			[
				new EmployeeDashboardAttendanceFact(
					new DateOnly(2026, 5, 1),
					EmployeeAttendanceStatusType.Present),
				new EmployeeDashboardAttendanceFact(
					new DateOnly(2026, 5, 2),
					EmployeeAttendanceStatusType.HalfDayLeave),
				new EmployeeDashboardAttendanceFact(
					new DateOnly(2026, 5, 3),
					EmployeeAttendanceStatusType.FullDayLeave)
			]);

		result.ShouldBe(50m);
	}

	[Fact]
	public void Today_Status_Should_Prefer_Recorded_Attendance_Over_Calendar_Fallbacks()
	{
		var result = EmployeeDashboardProjection.ResolveTodayStatus(
			new EmployeeDashboardAttendanceFact(
				new DateOnly(2026, 5, 1),
				EmployeeAttendanceStatusType.Present),
			isHoliday: true,
			isWeeklyOff: true);

		result.ShouldBe(EmployeeDashboardStatus.Present);
	}

	[Fact]
	public void Approved_Leave_Should_Fill_Unrecorded_Days_Without_Overriding_Attendance()
	{
		var result = EmployeeDashboardProjection.MergeAttendanceWithApprovedLeave(
			new DateOnly(2026, 5, 1),
			new DateOnly(2026, 5, 31),
			[
				new EmployeeDashboardAttendanceFact(
					new DateOnly(2026, 5, 12),
					EmployeeAttendanceStatusType.Present)
			],
			[
				new EmployeeDashboardApprovedLeaveFact(
					new DateOnly(2026, 5, 11),
					new DateOnly(2026, 5, 12),
					HalfDay: false),
				new EmployeeDashboardApprovedLeaveFact(
					new DateOnly(2026, 5, 20),
					new DateOnly(2026, 5, 20),
					HalfDay: true)
			]);

		result.Count.ShouldBe(3);
		var approvedFullDayLeave = result.Single(row =>
			row.Date == new DateOnly(2026, 5, 11));
		approvedFullDayLeave.Status
			.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave);
		approvedFullDayLeave.Source
			.ShouldBe(EmployeeDashboardAttendanceFactSource.ApprovedLeave);
		EmployeeDashboardProjection.ToStatus(approvedFullDayLeave)
			.ShouldBe(EmployeeDashboardStatus.FullDayLeave);
		var recordedAttendance = result.Single(row =>
			row.Date == new DateOnly(2026, 5, 12));
		recordedAttendance.Status
			.ShouldBe(EmployeeAttendanceStatusType.Present);
		recordedAttendance.Source
			.ShouldBe(EmployeeDashboardAttendanceFactSource.Attendance);
		var approvedHalfDayLeave = result.Single(row =>
			row.Date == new DateOnly(2026, 5, 20));
		approvedHalfDayLeave.Status
			.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
		approvedHalfDayLeave.Source
			.ShouldBe(EmployeeDashboardAttendanceFactSource.ApprovedLeave);
		EmployeeDashboardProjection.ToStatus(approvedHalfDayLeave)
			.ShouldBe(EmployeeDashboardStatus.HalfDayLeave);
		EmployeeDashboardProjection.CalculateAttendancePercentage(result).ShouldBe(100m);
	}

	[Fact]
	public void Recorded_Absence_And_Approved_Leave_Should_Stay_Distinct()
	{
		var absence = new EmployeeDashboardAttendanceFact(
			new DateOnly(2026, 5, 1),
			EmployeeAttendanceStatusType.FullDayLeave);
		var approvedLeave = new EmployeeDashboardAttendanceFact(
			new DateOnly(2026, 5, 2),
			EmployeeAttendanceStatusType.FullDayLeave,
			EmployeeDashboardAttendanceFactSource.ApprovedLeave);

		EmployeeDashboardProjection.ToStatus(absence)
			.ShouldBe(EmployeeDashboardStatus.Absent);
		EmployeeDashboardProjection.ToStatus(approvedLeave)
			.ShouldBe(EmployeeDashboardStatus.FullDayLeave);

		var trend = EmployeeDashboardProjection.BuildTrend(
			new DateOnly(2026, 5, 1),
			new DateOnly(2026, 5, 31),
			[absence, approvedLeave]);

		trend.Single().Absent.ShouldBe(1);
		trend.Single().FullDayLeave.ShouldBe(1);
	}

	[Fact]
	public void Custom_Weekly_Off_Should_Use_DotNet_Day_Of_Week_Numbers()
	{
		EmployeeDashboardProjection.IsWeeklyOff(
			DayOfWeek.Wednesday,
			AttendanceWeeklyOffPattern.Custom,
			[0, 3]).ShouldBeTrue();
		EmployeeDashboardProjection.IsWeeklyOff(
			DayOfWeek.Thursday,
			AttendanceWeeklyOffPattern.Custom,
			[0, 3]).ShouldBeFalse();
	}

	[Fact]
	public void Trend_Should_Include_Empty_Months_Inside_The_Academic_Range()
	{
		var result = EmployeeDashboardProjection.BuildTrend(
			new DateOnly(2026, 4, 1),
			new DateOnly(2026, 6, 30),
			new List<EmployeeDashboardAttendanceFact>
			{
				new(new DateOnly(2026, 4, 2), EmployeeAttendanceStatusType.Present),
				new(new DateOnly(2026, 6, 3), EmployeeAttendanceStatusType.HalfDayLeave)
			});

		result.Count.ShouldBe(3);
		result[0].Month.ShouldBe("2026-04");
		result[0].Present.ShouldBe(1);
		result[1].Month.ShouldBe("2026-05");
		result[1].Present.ShouldBe(0);
		result[2].HalfDay.ShouldBe(1);
	}
}
