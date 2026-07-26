using System;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardAttendanceProjection_Tests
{
	[Fact]
	public void Approved_Leave_Without_Attendance_Should_Not_Be_Missing_Or_Depress_Rate()
	{
		var today = new DateOnly(2026, 7, 26);
		var presentEmployeeId = Guid.NewGuid();
		var leaveEmployeeId = Guid.NewGuid();

		var result = HRDashboardAttendanceProjection.BuildAttendanceToday(
			[
				new AttendanceTrendRow(
					presentEmployeeId,
					today,
					EmployeeAttendanceStatusType.Present)
			],
			[
				new LeaveTrendRow(leaveEmployeeId, today, today)
			],
			today,
			activeStaffCount: 3);

		result.Present.ShouldBe(1);
		result.Absent.ShouldBe(0);
		result.MissingAttendance.ShouldBe(1);
		result.AttendanceRate.ShouldBe(50m);
	}

	[Fact]
	public void Stored_Attendance_Should_Override_Leave_Without_Double_Counting()
	{
		var today = new DateOnly(2026, 7, 26);
		var absentEmployeeId = Guid.NewGuid();
		var leaveOnlyEmployeeId = Guid.NewGuid();
		var attendanceRows = new[]
		{
			new AttendanceTrendRow(
				absentEmployeeId,
				today,
				EmployeeAttendanceStatusType.FullDayLeave)
		};
		var approvedLeaves = new[]
		{
			new LeaveTrendRow(absentEmployeeId, today, today),
			new LeaveTrendRow(leaveOnlyEmployeeId, today, today)
		};

		var todayResult = HRDashboardAttendanceProjection.BuildAttendanceToday(
			attendanceRows,
			approvedLeaves,
			today,
			activeStaffCount: 2);
		var trendResult = HRDashboardAttendanceProjection.BuildAttendanceTrend(
			attendanceRows,
			approvedLeaves,
			today,
			today);

		todayResult.Absent.ShouldBe(1);
		todayResult.MissingAttendance.ShouldBe(0);
		todayResult.AttendanceRate.ShouldBe(0m);
		trendResult.Single().Absent.ShouldBe(1);
		trendResult.Single().Leave.ShouldBe(1);
	}

	[Fact]
	public void Half_Day_Leave_Should_Only_Remove_Half_An_Attendance_Unit()
	{
		var today = new DateOnly(2026, 7, 26);
		var presentEmployeeId = Guid.NewGuid();
		var halfDayLeaveEmployeeId = Guid.NewGuid();
		var duplicateHalfDayLeaves = new[]
		{
			new LeaveTrendRow(
				halfDayLeaveEmployeeId,
				today,
				today,
				HalfDay: true),
			new LeaveTrendRow(
				halfDayLeaveEmployeeId,
				today,
				today,
				HalfDay: true)
		};

		var result = HRDashboardAttendanceProjection.BuildAttendanceToday(
			[
				new AttendanceTrendRow(
					presentEmployeeId,
					today,
					EmployeeAttendanceStatusType.Present)
			],
			duplicateHalfDayLeaves,
			today,
			activeStaffCount: 2);
		var trend = HRDashboardAttendanceProjection.BuildAttendanceTrend(
			[],
			duplicateHalfDayLeaves,
			today,
			today);

		result.MissingAttendance.ShouldBe(1);
		result.AttendanceRate.ShouldBe(66.67m);
		trend.Single().Leave.ShouldBe(1);
	}
}
