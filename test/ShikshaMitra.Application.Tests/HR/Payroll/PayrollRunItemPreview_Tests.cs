using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollRunItemPreview_Tests
{
	[Fact]
	public void Overtime_Should_Use_Attendance_Clock_Time_Independently_Of_The_Server_Timezone()
	{
		var attendance = CreateAttendance(
			new DateTimeOffset(2027, 3, 1, 7, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2027, 3, 1, 19, 0, 0, TimeSpan.Zero));

		AssertNinetyMinuteOvertime(attendance);
	}

	[Fact]
	public void Overtime_Should_Use_The_Canonical_Clock_When_Ef_Projects_A_Local_Server_Offset()
	{
		var serverOffset = TimeSpan.FromHours(-8);
		var attendance = CreateAttendance(
			new DateTimeOffset(2027, 2, 28, 23, 0, 0, serverOffset),
			new DateTimeOffset(2027, 3, 1, 11, 0, 0, serverOffset));

		AssertNinetyMinuteOvertime(attendance);
	}

	private static EmployeeAttendance CreateAttendance(DateTimeOffset timeIn, DateTimeOffset timeOut)
	{
		return new EmployeeAttendance
		{
			EmployeeId = Guid.NewGuid(),
			AttendanceDate = new DateOnly(2027, 3, 1),
			StatusType = EmployeeAttendanceStatusType.Present,
			TimeIn = timeIn,
			TimeOut = timeOut
		};
	}

	private static void AssertNinetyMinuteOvertime(EmployeeAttendance attendance)
	{
		var component = new EmployeePayrollComponent(
			Guid.NewGuid(),
			attendance.EmployeeId,
			"Basic Pay",
			componentId: null,
			description: null,
			amount: 30_000m,
			isFixed: true,
			isTemp: false,
			applicableMonth: new DateTime(2027, 3, 1),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning: true,
			usesMasterValue: false);
		var shift = new ShiftManagement
		{
			ShiftName = "Overtime shift",
			StartTime = new TimeOnly(7, 0),
			EndTime = new TimeOnly(17, 30)
		};
		var policy = new AttendancePolicy(
			Guid.NewGuid(),
			tenantId: Guid.NewGuid(),
			academicYearId: Guid.NewGuid(),
			workCalendarPolicyId: Guid.NewGuid(),
			"Overtime policy",
			HrPolicyStatus.Active,
			isDefault: false,
			AttendanceCaptureMode.ManualWithTime,
			fullDayThresholdPercentage: 75,
			halfDayThresholdPercentage: 50,
			absentThresholdPercentage: 0,
			AttendanceMissingPunchBehavior.RequiresReview,
			manualCorrectionAllowed: true,
			graceTime: 0,
			maxLateAllowedPerMonth: 0,
			AttendancePenaltyType.None,
			penaltyCountThreshold: 0,
			earlyExitEnabled: false,
			earlyExitGraceTime: 0,
			overtimeEnabled: true,
			AttendanceOvertimeRate.TwoX,
			minOvertimeMinutes: 30,
			AttendanceHolidayWorkBehavior.Paid,
			payrollPreviewEnabled: true);

		var preview = PayrollRunItemPreview.Prepare(
			[attendance],
			[component],
			[],
			new PayrollBasisDaysResult(27m, DayCalculationType.WorkingDays, false),
			2027,
			3,
			[],
			leaveUnitsByDate: null,
			previousMonthNetPay: null,
			employmentStartDate: null,
			shift,
			policy);

		var overtime = preview.Computation.Earnings.Single(x => x.Source.Name == "Overtime Pay");
		overtime.Source.Description.ShouldBe("90 overtime minutes");
		overtime.Amount.ShouldBeGreaterThan(0m);
	}
}
