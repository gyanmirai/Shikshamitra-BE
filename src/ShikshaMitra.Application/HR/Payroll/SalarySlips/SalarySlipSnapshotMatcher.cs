using System;
using System.Collections.Generic;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class SalarySlipSnapshotMatcher
{
	public static bool SnapshotMatchesResolvedPolicies(
		PayrollRunItemSnapshot snapshot,
		HrResolvedPolicies resolvedPolicies) =>
		snapshot.PayrollPolicyId == resolvedPolicies.PayrollPolicy.Id &&
		snapshot.PayrollPolicyVersion == resolvedPolicies.PayrollPolicy.Version &&
		snapshot.AttendancePolicyId == resolvedPolicies.AttendancePolicy.Id &&
		snapshot.AttendancePolicyVersion == resolvedPolicies.AttendancePolicy.Version &&
		snapshot.WorkCalendarPolicyId == resolvedPolicies.PayrollWorkCalendarPolicy.Id &&
		snapshot.WorkCalendarPolicyVersion == resolvedPolicies.PayrollWorkCalendarPolicy.Version;

	public static bool SalarySlipMatchesSnapshot(
		SalarySlip salarySlip,
		PayrollRunItemSnapshot snapshot,
		IReadOnlyList<SalarySlipLineItem> snapshotEarnings,
		IReadOnlyList<SalarySlipLineItem> snapshotDeductions) =>
		SalarySlipPolicyMetadataMatchesSnapshot(salarySlip, snapshot) &&
		SalarySlipOutputMatchesSnapshot(salarySlip, snapshot, snapshotEarnings, snapshotDeductions);

	public static bool SalarySlipFinancialLinesMatchSnapshot(
		SalarySlip salarySlip,
		PayrollRunItemSnapshot snapshot,
		IReadOnlyList<SalarySlipLineItem> snapshotEarnings,
		IReadOnlyList<SalarySlipLineItem> snapshotDeductions) =>
		Math.Round(salarySlip.TotalEarning, 2, MidpointRounding.AwayFromZero) == Math.Round(snapshot.NetPay + snapshot.TotalDeduction, 2, MidpointRounding.AwayFromZero) &&
		Math.Round(salarySlip.TotalDeduction, 2, MidpointRounding.AwayFromZero) == Math.Round(snapshot.TotalDeduction, 2, MidpointRounding.AwayFromZero) &&
		Math.Round(salarySlip.NetSalary, 2, MidpointRounding.AwayFromZero) == Math.Round(snapshot.NetPay, 2, MidpointRounding.AwayFromZero) &&
		LineItemsMatch(salarySlip.Earnings, snapshotEarnings) &&
		LineItemsMatch(salarySlip.Deductions, snapshotDeductions);

	public static DayCalculationType ParseDayCalculationMode(string value) => value switch
	{
		"working-days" => DayCalculationType.WorkingDays,
		"total-month-days" => DayCalculationType.TotalMonthDays,
		"manual-days" => DayCalculationType.ManualDays,
		_ => throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat)
			.WithData("reason", "Invalid payroll day calculation mode on salary slip snapshot.")
			.WithData("dayCalculationMode", value)
	};

	private static bool SalarySlipPolicyMetadataMatchesSnapshot(
		SalarySlip salarySlip,
		PayrollRunItemSnapshot snapshot) =>
		salarySlip.PayrollPolicyId == snapshot.PayrollPolicyId &&
		salarySlip.PayrollPolicyVersion == snapshot.PayrollPolicyVersion &&
		salarySlip.AttendancePolicyId == snapshot.AttendancePolicyId &&
		salarySlip.AttendancePolicyVersion == snapshot.AttendancePolicyVersion &&
		salarySlip.WorkCalendarPolicyId == snapshot.WorkCalendarPolicyId &&
		salarySlip.WorkCalendarPolicyVersion == snapshot.WorkCalendarPolicyVersion;

	private static bool SalarySlipOutputMatchesSnapshot(
		SalarySlip salarySlip,
		PayrollRunItemSnapshot snapshot,
		IReadOnlyList<SalarySlipLineItem> snapshotEarnings,
		IReadOnlyList<SalarySlipLineItem> snapshotDeductions) =>
		salarySlip.PayrollBasisDays == snapshot.BasisDays &&
		salarySlip.PayableDays == snapshot.PayableDays &&
		salarySlip.ProrationFactor == snapshot.ProrationFactor &&
		salarySlip.PayrollDayCalculationMode == ParseDayCalculationMode(snapshot.DayCalculationMode) &&
		SalarySlipFinancialLinesMatchSnapshot(salarySlip, snapshot, snapshotEarnings, snapshotDeductions);

	private static bool LineItemsMatch(
		IReadOnlyList<SalarySlipLineItem> current,
		IReadOnlyList<SalarySlipLineItem> snapshot)
	{
		if (current.Count != snapshot.Count)
		{
			return false;
		}

		for (var i = 0; i < current.Count; i++)
		{
			if (ToLineComparisonKey(current[i]) != ToLineComparisonKey(snapshot[i]))
			{
				return false;
			}
		}

		return true;
	}

	private static SalarySlipLineComparisonKey ToLineComparisonKey(SalarySlipLineItem item) =>
		new(
			item.Name ?? string.Empty,
			Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero),
			item.Description ?? string.Empty,
			item.IsFixed,
			item.IsTemp,
			item.ComponentType,
			item.DependOn,
			item.FullMonthAmount,
			item.FormulaType,
			item.FormulaValue,
			item.BasisComponentId);

	private readonly record struct SalarySlipLineComparisonKey(
		string Name,
		decimal Amount,
		string Description,
		bool IsFixed,
		bool IsTemp,
		SalaryComponentType ComponentType,
		SalaryComponentDependency DependOn,
		decimal? FullMonthAmount,
		string? FormulaType,
		decimal? FormulaValue,
		Guid? BasisComponentId);
}
