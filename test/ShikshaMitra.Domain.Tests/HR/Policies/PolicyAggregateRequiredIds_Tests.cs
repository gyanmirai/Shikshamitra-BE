using System;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class PolicyAggregateRequiredIds_Tests
{
	[Fact]
	public void WorkCalendarPolicy_Should_Reject_Missing_Academic_Year()
	{
		var ex = Should.Throw<BusinessException>(() => new WorkCalendarPolicy(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.Empty,
			"Calendar",
			HrPolicyStatus.Draft,
			null,
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			[],
			WorkCalendarHolidaySyncMode.LinkedWithOverrides));

		ex.Code.ShouldBe(HRErrorCodes.AcademicYearRequired);
	}

	[Fact]
	public void WorkCalendarPolicy_Should_Reject_Custom_Weekly_Off_Without_Days()
	{
		var ex = Should.Throw<BusinessException>(() => new WorkCalendarPolicy(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Calendar",
			HrPolicyStatus.Draft,
			null,
			null,
			AttendanceWeeklyOffPattern.Custom,
			[],
			WorkCalendarHolidaySyncMode.LinkedWithOverrides));

		ex.Code.ShouldBe(HRErrorCodes.InvalidCustomWeeklyOff);
	}

	[Fact]
	public void PayrollPolicy_Should_Reject_Missing_Required_Ids()
	{
		var missingAcademicYear = Should.Throw<BusinessException>(() => CreatePayrollPolicy(academicYearId: Guid.Empty));
		missingAcademicYear.Code.ShouldBe(HRErrorCodes.AcademicYearRequired);

		var missingWorkCalendar = Should.Throw<BusinessException>(() => CreatePayrollPolicy(workCalendarPolicyId: Guid.Empty));
		missingWorkCalendar.Code.ShouldBe(HRErrorCodes.WorkCalendarPolicyRequired);
	}

	[Fact]
	public void AttendancePolicy_Should_Reject_Missing_Required_Ids()
	{
		var missingAcademicYear = Should.Throw<BusinessException>(() => CreateAttendancePolicy(academicYearId: Guid.Empty));
		missingAcademicYear.Code.ShouldBe(HRErrorCodes.AcademicYearRequired);

		var missingWorkCalendar = Should.Throw<BusinessException>(() => CreateAttendancePolicy(workCalendarPolicyId: Guid.Empty));
		missingWorkCalendar.Code.ShouldBe(HRErrorCodes.WorkCalendarPolicyRequired);
	}

	[Fact]
	public void AttendancePolicy_Should_Reject_Salary_Deduction_Without_Amount()
	{
		var missingAmount = Should.Throw<BusinessException>(() => CreateAttendancePolicy(
			penaltyType: AttendancePenaltyType.SalaryDeduction,
			penaltyDeductionAmount: null));
		missingAmount.Code.ShouldBe(HRErrorCodes.InvalidAttendancePolicyRule);

		var zeroAmount = Should.Throw<BusinessException>(() => CreateAttendancePolicy(
			penaltyType: AttendancePenaltyType.SalaryDeduction,
			penaltyDeductionAmount: 0m));
		zeroAmount.Code.ShouldBe(HRErrorCodes.InvalidAttendancePolicyRule);
	}

	[Fact]
	public void Assignment_Should_Reject_Missing_Required_Ids()
	{
		var missingAcademicYear = Should.Throw<BusinessException>(() => CreateAssignment(academicYearId: Guid.Empty));
		missingAcademicYear.Code.ShouldBe(HRErrorCodes.AcademicYearRequired);

		var missingAttendance = Should.Throw<BusinessException>(() => CreateAssignment(attendancePolicyId: Guid.Empty));
		missingAttendance.Code.ShouldBe(HRErrorCodes.AttendancePolicyRequired);

		var missingPayroll = Should.Throw<BusinessException>(() => CreateAssignment(payrollPolicyId: Guid.Empty));
		missingPayroll.Code.ShouldBe(HRErrorCodes.PayrollPolicyRequired);
	}

	[Fact]
	public void Assignment_Should_Reject_Invalid_Effective_Date_And_Priority()
	{
		var missingEffectiveFrom = Should.Throw<BusinessException>(() => CreateAssignment(effectiveFrom: DateTime.MinValue));
		missingEffectiveFrom.Code.ShouldBe(HRErrorCodes.PolicyEffectiveFromRequired);

		var invalidRange = Should.Throw<BusinessException>(() => CreateAssignment(
			effectiveFrom: new DateTime(2026, 6, 1),
			effectiveTo: new DateTime(2026, 5, 31)));
		invalidRange.Code.ShouldBe(HRErrorCodes.PolicyAssignmentRangeInvalid);

		var invalidPriority = Should.Throw<BusinessException>(() => CreateAssignment(priority: 1001));
		invalidPriority.Code.ShouldBe(HRErrorCodes.PolicyAssignmentPriorityInvalid);
	}

	[Fact]
	public void WorkCalendarHoliday_Should_Reject_Missing_Required_Ids()
	{
		var missingAcademicYear = Should.Throw<BusinessException>(() => CreateHoliday(academicYearId: Guid.Empty));
		missingAcademicYear.Code.ShouldBe(HRErrorCodes.AcademicYearRequired);

		var missingWorkCalendar = Should.Throw<BusinessException>(() => CreateHoliday(workCalendarPolicyId: Guid.Empty));
		missingWorkCalendar.Code.ShouldBe(HRErrorCodes.WorkCalendarPolicyRequired);
	}

	[Fact]
	public void Policy_Aggregates_Should_Reject_Archived_Status_In_Update_Payloads()
	{
		var calendarCreate = Should.Throw<BusinessException>(() => new WorkCalendarPolicy(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Calendar",
			HrPolicyStatus.Archived,
			null,
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			[],
			WorkCalendarHolidaySyncMode.LinkedWithOverrides));
		var payrollCreate = Should.Throw<BusinessException>(() => CreatePayrollPolicy(status: HrPolicyStatus.Archived));
		var attendanceCreate = Should.Throw<BusinessException>(() => CreateAttendancePolicy(status: HrPolicyStatus.Archived));
		var attendanceDefaultCreate = Should.Throw<BusinessException>(() => CreateAttendancePolicy(status: HrPolicyStatus.Archived, isDefault: true));

		var calendar = new WorkCalendarPolicy(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Calendar",
			HrPolicyStatus.Draft,
			null,
			null,
			AttendanceWeeklyOffPattern.SaturdaySunday,
			[],
			WorkCalendarHolidaySyncMode.LinkedWithOverrides);
		var payroll = CreatePayrollPolicy();
		var attendance = CreateAttendancePolicy();

		var calendarUpdate = Should.Throw<BusinessException>(() => calendar.Update(
			calendar.Name,
			HrPolicyStatus.Archived,
			calendar.EffectiveFrom,
			calendar.EffectiveTo,
			calendar.WeeklyOffPattern,
			calendar.GetCustomWeeklyOffs(),
			calendar.HolidaySyncMode));
		var payrollUpdate = Should.Throw<BusinessException>(() => payroll.Update(
			payroll.WorkCalendarPolicyId,
			payroll.Name,
			HrPolicyStatus.Archived,
			false,
			payroll.DayCalculationType,
			payroll.ManualDays,
			payroll.GetManualMonthDays(),
			payroll.SaturdayWorkPolicy,
			payroll.GetHolidayOverrides()));
		var attendanceUpdate = Should.Throw<BusinessException>(() => attendance.Update(
			attendance.WorkCalendarPolicyId,
			attendance.Name,
			HrPolicyStatus.Archived,
			false,
			attendance.CaptureMode,
			attendance.FullDayThresholdPercentage,
			attendance.HalfDayThresholdPercentage,
			attendance.AbsentThresholdPercentage,
			attendance.MissingPunchBehavior,
			attendance.ManualCorrectionAllowed,
			attendance.GraceTime,
			attendance.MaxLateAllowedPerMonth,
			attendance.PenaltyType,
			attendance.PenaltyCountThreshold,
			attendance.EarlyExitEnabled,
			attendance.EarlyExitGraceTime,
			attendance.OvertimeEnabled,
			attendance.OvertimeRate,
			attendance.MinOvertimeMinutes,
			attendance.HolidayWorkBehavior,
			attendance.PayrollPreviewEnabled));

		calendarCreate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		payrollCreate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		attendanceCreate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		attendanceDefaultCreate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		calendarUpdate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		payrollUpdate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		attendanceUpdate.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);

		calendar.Archive();
		payroll.Archive();
		attendance.Archive();

		calendar.Status.ShouldBe(HrPolicyStatus.Archived);
		payroll.Status.ShouldBe(HrPolicyStatus.Archived);
		attendance.Status.ShouldBe(HrPolicyStatus.Archived);
	}

	private static PayrollPolicy CreatePayrollPolicy(
		Guid? academicYearId = null,
		Guid? workCalendarPolicyId = null,
		HrPolicyStatus status = HrPolicyStatus.Draft) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			academicYearId ?? Guid.NewGuid(),
			workCalendarPolicyId ?? Guid.NewGuid(),
			"Payroll",
			status,
			false,
			DayCalculationType.WorkingDays,
			null,
			[],
			SaturdayWorkPolicy.FullDay,
			[]);

	private static AttendancePolicy CreateAttendancePolicy(
		Guid? academicYearId = null,
		Guid? workCalendarPolicyId = null,
		HrPolicyStatus status = HrPolicyStatus.Draft,
		bool isDefault = false,
		AttendancePenaltyType penaltyType = AttendancePenaltyType.None,
		decimal? penaltyDeductionAmount = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			academicYearId ?? Guid.NewGuid(),
			workCalendarPolicyId ?? Guid.NewGuid(),
			"Attendance",
			status,
			isDefault,
			AttendanceCaptureMode.Integrated,
			75,
			50,
			0,
			AttendanceMissingPunchBehavior.RequiresReview,
			true,
			0,
			0,
			penaltyType,
			0,
			false,
			0,
			false,
			null,
			null,
			AttendanceHolidayWorkBehavior.CompOff,
			true,
			penaltyDeductionAmount);

	private static HrPolicyAssignment CreateAssignment(
		Guid? academicYearId = null,
		Guid? attendancePolicyId = null,
		Guid? payrollPolicyId = null,
		DateTime? effectiveFrom = null,
		DateTime? effectiveTo = null,
		int priority = 100) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			academicYearId ?? Guid.NewGuid(),
			HrPolicyAssignmentTargetType.Employee,
			Guid.NewGuid().ToString("D"),
			"Employee",
			attendancePolicyId ?? Guid.NewGuid(),
			payrollPolicyId ?? Guid.NewGuid(),
			null,
			effectiveFrom ?? new DateTime(2026, 4, 1),
			effectiveTo,
			true,
			priority,
			"Required ids regression");

	private static WorkCalendarHoliday CreateHoliday(Guid? academicYearId = null, Guid? workCalendarPolicyId = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			academicYearId ?? Guid.NewGuid(),
			workCalendarPolicyId ?? Guid.NewGuid(),
			new DateOnly(2026, 5, 1),
			null,
			"May Day",
			WorkCalendarHolidaySource.Manual,
			null,
			true,
			null);
}
