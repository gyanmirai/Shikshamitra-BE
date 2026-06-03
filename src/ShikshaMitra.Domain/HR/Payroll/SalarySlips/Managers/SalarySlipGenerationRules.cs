using System;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public static class SalarySlipGenerationRules
{
	public static string NormalizeMonthYear(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new BusinessException(HRErrorCodes.MonthYearRequired);
		}

		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var normalizedMonthYear))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		return normalizedMonthYear;
	}

	public static void EnsureEmployeeEligibleForPayrollMonth(Employee employee, int year, int month)
	{
		var monthStartDate = new DateOnly(year, month, 1);
		var monthEndDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
		if (EmployeeLifecycleEligibility.IsEligibleForPayrollMonth(employee, monthStartDate, monthEndDate))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.SalarySlipEmployeeNotActiveInMonth)
			.WithData("employeeId", employee.Id)
			.WithData("month", $"{year}-{month:00}")
			.WithData("effectiveStartDate", GetEffectiveEmploymentStartDate(employee))
			.WithData("statusEffectiveDate", employee.StatusEffectiveDate?.ToString("O") ?? string.Empty)
			.WithData("statusLastChangedAt", employee.StatusLastChangedAt?.ToString("O") ?? string.Empty);
	}

	public static DateOnly GetEffectiveEmploymentStartDate(Employee employee) =>
		EmployeeLifecycleEligibility.GetEffectiveEmploymentStartDate(employee);

	public static void ApplySnapshotMetadata(
		SalarySlip salarySlip,
		PayrollRunItemSnapshot snapshot,
		DayCalculationType dayCalculationMode)
	{
		salarySlip.SetPayrollComputationSnapshot(
			snapshot.BasisDays,
			snapshot.PayableDays,
			snapshot.ProrationFactor,
			dayCalculationMode);
		salarySlip.SetPolicySnapshot(
			snapshot.PayrollPolicyId,
			snapshot.PayrollPolicyVersion,
			snapshot.AttendancePolicyId,
			snapshot.AttendancePolicyVersion,
			snapshot.WorkCalendarPolicyId,
			snapshot.WorkCalendarPolicyVersion);
	}

	public static void ApplyPolicySnapshot(SalarySlip salarySlip, HrResolvedPolicies resolvedPolicies)
	{
		salarySlip.SetPolicySnapshot(
			resolvedPolicies.PayrollPolicy.Id,
			resolvedPolicies.PayrollPolicy.Version,
			resolvedPolicies.AttendancePolicy.Id,
			resolvedPolicies.AttendancePolicy.Version,
			resolvedPolicies.PayrollWorkCalendarPolicy.Id,
			resolvedPolicies.PayrollWorkCalendarPolicy.Version);
	}
}
