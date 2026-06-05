using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public static class PayrollRunRules
{
	public static (int Year, int Month, string InternalFormat) ParseMonth(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new BusinessException(HRErrorCodes.MonthYearRequired);
		}

		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var internalFormat))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat)
				.WithData("providedMonth", monthYear)
				.WithData("acceptedFormats", "YYYY-MM, MMMM-YYYY, or date-like values");
		}

		var year = int.Parse(internalFormat[..4]);
		var month = int.Parse(internalFormat[5..7]);
		return (year, month, internalFormat);
	}

	public static string GetPreviousMonth(string internalMonthYear)
	{
		var year = int.Parse(internalMonthYear[..4]);
		var month = int.Parse(internalMonthYear[5..7]);
		return new DateTime(year, month, 1).AddMonths(-1).ToString("yyyy-MM");
	}

	public static List<Guid> NormalizeReviewEmployeeIds(List<Guid>? employeeIds)
	{
		var normalized = employeeIds?
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList() ?? new List<Guid>();
		if (normalized.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollReviewEmployeeIdsRequired);
		}

		return normalized;
	}

	public static DayCalculationType ParseDayCalculationMode(string value) => value switch
	{
		"working-days" => DayCalculationType.WorkingDays,
		"total-month-days" => DayCalculationType.TotalMonthDays,
		"manual-days" => DayCalculationType.ManualDays,
		_ => throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
			.WithData("reason", "Invalid payroll day calculation mode on reviewed snapshot.")
			.WithData("dayCalculationMode", value)
	};

	public static bool IsPayrollPolicySetupException(BusinessException ex) =>
		ex.Code is
			HRErrorCodes.DefaultPayrollPolicyRequired or
			HRErrorCodes.DefaultAttendancePolicyRequired or
			HRErrorCodes.AcademicYearRequiredForPolicyResolution or
			HRErrorCodes.WorkCalendarPolicyMustBeActive or
			HRErrorCodes.HrPolicyMustBeActive or
			HRErrorCodes.HrPolicyInvalidWorkCalendar;

	public static string FormatEmployeeName(Employee employee)
	{
		if (!string.IsNullOrWhiteSpace(employee.EmployeeName))
		{
			return employee.EmployeeName;
		}

		var parts = new[] { employee.FirstName, employee.MiddleName, employee.LastName }
			.Where(s => !string.IsNullOrWhiteSpace(s));
		return string.Join(" ", parts);
	}

	public static DateOnly GetEffectiveEmploymentStartDate(Employee employee) =>
		EmployeeLifecycleEligibility.GetEffectiveEmploymentStartDate(employee);

	public static DateOnly GetPayrollMonthEndDate(int year, int month) =>
		new(year, month, DateTime.DaysInMonth(year, month));

	public static DateTime GetPayrollPolicyResolutionDate(int year, int month) =>
		new(year, month, DateTime.DaysInMonth(year, month));
}
