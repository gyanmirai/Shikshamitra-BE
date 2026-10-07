using System;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipCorrectionAttendanceRules_Tests
{
	[Theory]
	[InlineData("2026-10-01T09:00:00Z", "2026-10-11T19:00:00Z", "timeOut")]
	[InlineData("2026-09-30T09:00:00Z", "2026-10-01T17:00:00Z", "timeIn")]
	[InlineData("2026-10-01T09:00:00Z", "2026-10-02T00:00:00Z", "timeOut")]
	[InlineData("2026-10-01T09:00:00Z", "2026-10-01T08:59:00Z", "timeOut")]
	[InlineData("2026-10-01T09:00:00Z", "2026-10-01T09:00:00Z", "timeOut")]
	[InlineData(null, "2026-10-11T19:00:00Z", "timeOut")]
	[InlineData("2026-10-01T00:30:00+05:30", null, "timeIn")]
	public void Rejects_punches_that_do_not_fit_the_attendance_day(string? timeIn, string? timeOut, string field)
	{
		var exception = Should.Throw<BusinessException>(() => PayslipCorrectionAttendanceRules.EnsureValidPunches(Day(timeIn, timeOut)));
		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationInvalidPunches);
		exception.Data["field"].ShouldBe(field);
		exception.Data["attendanceDate"].ShouldBe("2026-10-01");
	}

	[Theory]
	[InlineData(null, null)]
	[InlineData("2026-10-01T09:00:00Z", null)]
	[InlineData(null, "2026-10-01T17:00:00Z")]
	[InlineData("2026-10-01T09:00:00Z", "2026-10-01T19:00:00Z")]
	[InlineData("2026-10-01T00:00:00Z", "2026-10-01T23:59:59Z")]
	[InlineData("2026-10-01T14:30:00+05:30", "2026-10-01T22:30:00+05:30")]
	public void Preserves_status_only_missing_punch_and_valid_same_day_inputs(string? timeIn, string? timeOut)
	{
		var day = Day(timeIn, timeOut);
		var beforeIn = day.TimeIn;
		var beforeOut = day.TimeOut;
		Should.NotThrow(() => PayslipCorrectionAttendanceRules.EnsureValidPunches(day));
		day.TimeIn.ShouldBe(beforeIn);
		day.TimeOut.ShouldBe(beforeOut);
	}

	[Fact]
	public void Preserves_explicit_absent_midnight_but_rejects_present_midnight()
	{
		var absent = Day("2026-10-01T00:00:00Z", "2026-10-01T00:00:00Z");
		absent.StatusType = EmployeeAttendanceStatusType.FullDayLeave;
		Should.NotThrow(() => PayslipCorrectionAttendanceRules.EnsureValidPunches(absent));
		absent.StatusType = EmployeeAttendanceStatusType.Present;
		Should.Throw<BusinessException>(() => PayslipCorrectionAttendanceRules.EnsureValidPunches(absent));
	}

	private static EmployeeAttendance Day(string? timeIn, string? timeOut) => new()
	{
		AttendanceDate = new(2026, 10, 1), StatusType = EmployeeAttendanceStatusType.Present,
		TimeIn = timeIn == null ? null : DateTimeOffset.Parse(timeIn),
		TimeOut = timeOut == null ? null : DateTimeOffset.Parse(timeOut)
	};
}
