using System;
using System.Globalization;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using static ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipSnapshotMatcher;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipDraftFreshnessService(
	SalarySlipManager salarySlipManager,
	SalarySlipSnapshotReader snapshotReader,
	HrPolicyResolver hrPolicyResolver,
	PayrollCalendarResolver payrollCalendarResolver,
	IClock clock) : ITransientDependency
{
	public async Task<SalarySlip> EnsureFreshAsync(
		SalarySlip salarySlip,
		bool hadExistingSalarySlip)
	{
		if (salarySlip.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
		{
			return salarySlip;
		}

		var snapshot = await snapshotReader.GetAsync(salarySlip.EmployeeId, salarySlip.MonthYear);
		if (snapshot != null)
		{
			if (!snapshot.IsLocked)
			{
				if (!hadExistingSalarySlip)
				{
					return salarySlip;
				}

				var resolvedPolicies = await ResolvePoliciesForMonthAsync(salarySlip.EmployeeId, salarySlip.MonthYear);
				var unlockedSnapshotEarnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
				var unlockedSnapshotDeductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
				if (IsHistoricalPayrollMonth(salarySlip.MonthYear) &&
					SalarySlipFinancialLinesMatchSnapshot(salarySlip, snapshot, unlockedSnapshotEarnings, unlockedSnapshotDeductions))
				{
					return salarySlip;
				}

				if (!SnapshotMatchesResolvedPolicies(snapshot, resolvedPolicies) ||
				    !SalarySlipMatchesSnapshot(salarySlip, snapshot, unlockedSnapshotEarnings, unlockedSnapshotDeductions))
				{
					return await salarySlipManager.GenerateOrGetSalarySlipAsync(
						salarySlip.EmployeeId,
						salarySlip.MonthYear,
						forceRegenerate: true);
				}

				return salarySlip;
			}

			var snapshotEarnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
			var snapshotDeductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
			if (!SalarySlipMatchesSnapshot(salarySlip, snapshot, snapshotEarnings, snapshotDeductions))
			{
				return await salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
					snapshot,
					snapshotEarnings,
					snapshotDeductions,
					ParseDayCalculationMode(snapshot.DayCalculationMode));
			}

			return salarySlip;
		}

		if (hadExistingSalarySlip && IsHistoricalPayrollMonth(salarySlip.MonthYear))
		{
			return salarySlip;
		}

		return await salarySlipManager.GenerateOrGetSalarySlipAsync(
			salarySlip.EmployeeId,
			salarySlip.MonthYear,
			forceRegenerate: true);
	}

	private bool IsHistoricalPayrollMonth(string monthYear) =>
		string.Compare(
			NormalizeMonthYear(monthYear),
			clock.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
			StringComparison.Ordinal) < 0;

	private async Task<HrResolvedPolicies> ResolvePoliciesForMonthAsync(Guid employeeId, string monthYear)
	{
		var year = int.Parse(monthYear[..4], CultureInfo.InvariantCulture);
		var month = int.Parse(monthYear[5..7], CultureInfo.InvariantCulture);
		var calendar = await payrollCalendarResolver.ResolveAsync(year, month);
		return await hrPolicyResolver.ResolveAsync(
			employeeId,
			new DateTime(year, month, 1),
			calendar.AcademicYearId);
	}

	private static string NormalizeMonthYear(string monthYear)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var normalizedMonthYear))
		{
			throw new BusinessException(string.IsNullOrWhiteSpace(monthYear)
				? HRErrorCodes.MonthYearRequired
				: HRErrorCodes.InvalidMonthYearFormat);
		}

		return normalizedMonthYear;
	}
}
