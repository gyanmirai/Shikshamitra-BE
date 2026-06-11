using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollRunItemBuilder
{
	public static RunPayrollItem BuildPayrollItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		HrResolvedPolicies resolvedPolicies,
		string holidayImpactJson,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay,
		IReadOnlyList<string>? additionalIssueCodes = null,
		DateOnly? employmentStartDate = null)
	{
		var itemAttendancePolicyWarnings = new List<string>();
		var preview = PayrollRunItemPreview.Prepare(
			attendanceRecords,
			components,
			adjustments,
			basis,
			year,
			month,
			holidays,
			leaveUnitsByDate,
			previousMonthNetPay,
			employmentStartDate,
			employee.Shift,
			resolvedPolicies.AttendancePolicy);

		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = isLocked,
			ReviewStatus = isLocked ? ReviewStatuses.Locked : review == null ? ReviewStatuses.Pending : ReviewStatuses.Reviewed,
			ReviewedByUserId = review?.ReviewedByUserId,
			ReviewedAt = review?.ReviewedAt,
			EmployeeName = PayrollRunRules.FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)preview.Computation.BasicSalary,
			GrossSalary = (double)preview.Computation.GrossSalary,
			FullGrossSalary = (double)preview.Computation.FullGrossSalary,
			TotalDeduction = (double)preview.Computation.TotalDeduction,
			NetPay = (double)preview.Computation.NetPay,
			PreviousMonthNetPay = previousMonthNetPay.HasValue ? (double)previousMonthNetPay.Value : null,
			NetPayDelta = preview.NetPayDelta.HasValue ? (double)preview.NetPayDelta.Value : null,
			NetPayDeltaPercent = preview.NetPayDeltaPercent.HasValue ? (double)preview.NetPayDeltaPercent.Value : null,
			IssueCodes = (additionalIssueCodes ?? Array.Empty<string>())
				.Concat(PayrollIssueCodeBuilder.Build(payrollDetails, components, basis.BasisDays, preview.Computation.PayableDays, preview.Computation.NetPay, previousMonthNetPay, preview.NetPayDeltaPercent, preview.HasAttendanceOrPaidLeave))
				.Distinct()
				.ToList(),
			PresentDays = preview.AttendanceSummary.PresentDays,
			HalfWorkingDays = preview.AttendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)preview.Computation.PayableDays,
			ProrationFactor = (double)preview.Computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = basis.UsedTenantDefault,
			PayrollPolicyId = resolvedPolicies.PayrollPolicy.Id,
			PayrollPolicyVersion = resolvedPolicies.PayrollPolicy.Version,
			AttendancePolicyId = resolvedPolicies.AttendancePolicy.Id,
			AttendancePolicyVersion = resolvedPolicies.AttendancePolicy.Version,
			WorkCalendarPolicyId = resolvedPolicies.PayrollWorkCalendarPolicy.Id,
			WorkCalendarPolicyVersion = resolvedPolicies.PayrollWorkCalendarPolicy.Version,
			HolidayImpactJson = holidayImpactJson,
			BasisDays = (double)basis.BasisDays,
			PayableUnits = (double)preview.Computation.PayableDays,
			PayrollAttendanceSource = "raw_attendance",
			AttendancePolicyWarnings = itemAttendancePolicyWarnings,
			EarningComponents = preview.Computation.Earnings.Select(PayrollRunProjection.ToComponentItem).ToList(),
			DeductionComponents = preview.Computation.Deductions.Select(PayrollRunProjection.ToComponentItem).ToList(),
			PayrollAdjustments = adjustments.Select(PayrollRunProjection.ToAdjustmentDto).ToList()
		};
	}

	public static RunPayrollItem BuildPayrollSetupIssueItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		int year,
		int month,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		IReadOnlyList<string> issueCodes,
		DateOnly? employmentStartDate = null)
	{
		var basis = new PayrollBasisDaysResult(
			DateTime.DaysInMonth(year, month),
			DayCalculationType.TotalMonthDays,
			UsedTenantDefault: true,
			SaturdayWorkPolicy.FullDay);
		var preview = PayrollRunItemPreview.Prepare(
			attendanceRecords,
			components,
			adjustments,
			basis,
			year,
			month,
			Array.Empty<Holiday>(),
			leaveUnitsByDate,
			previousMonthNetPay,
			employmentStartDate);

		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = isLocked,
			ReviewStatus = isLocked ? ReviewStatuses.Locked : review == null ? ReviewStatuses.Pending : ReviewStatuses.Reviewed,
			ReviewedByUserId = review?.ReviewedByUserId,
			ReviewedAt = review?.ReviewedAt,
			EmployeeName = PayrollRunRules.FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)preview.Computation.BasicSalary,
			GrossSalary = (double)preview.Computation.GrossSalary,
			FullGrossSalary = (double)preview.Computation.FullGrossSalary,
			TotalDeduction = (double)preview.Computation.TotalDeduction,
			NetPay = (double)preview.Computation.NetPay,
			PreviousMonthNetPay = previousMonthNetPay.HasValue ? (double)previousMonthNetPay.Value : null,
			NetPayDelta = preview.NetPayDelta.HasValue ? (double)preview.NetPayDelta.Value : null,
			NetPayDeltaPercent = preview.NetPayDeltaPercent.HasValue ? (double)preview.NetPayDeltaPercent.Value : null,
			IssueCodes = issueCodes
				.Concat(PayrollIssueCodeBuilder.Build(payrollDetails, components, basis.BasisDays, preview.Computation.PayableDays, preview.Computation.NetPay, previousMonthNetPay, preview.NetPayDeltaPercent, preview.HasAttendanceOrPaidLeave))
				.Distinct()
				.ToList(),
			PresentDays = preview.AttendanceSummary.PresentDays,
			HalfWorkingDays = preview.AttendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)preview.Computation.PayableDays,
			ProrationFactor = (double)preview.Computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = true,
			HolidayImpactJson = "[]",
			BasisDays = (double)basis.BasisDays,
			PayableUnits = (double)preview.Computation.PayableDays,
			PayrollAttendanceSource = "raw_attendance",
			EarningComponents = preview.Computation.Earnings.Select(PayrollRunProjection.ToComponentItem).ToList(),
			DeductionComponents = preview.Computation.Deductions.Select(PayrollRunProjection.ToComponentItem).ToList(),
			PayrollAdjustments = adjustments.Select(PayrollRunProjection.ToAdjustmentDto).ToList()
		};
	}

	public static string BuildHolidayImpactJson(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		var items = PayrollMonthCalendar.GetResolvedHolidayDaysInMonth(year, month, holidays, holidayOverrides)
			.Select(x => new
			{
				date = x.Date.ToString("yyyy-MM-dd"),
				calendarHolidayId = x.CalendarHolidayId,
				holidayName = x.HolidayName,
				payrollImpact = "excluded",
				x.IsPayrollOnly
			})
			.ToList();

		return JsonSerializer.Serialize(items);
	}
}
