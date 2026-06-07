using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipSnapshotMatcher;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipDraftFreshnessService(
	SalarySlipManager salarySlipManager,
	SalarySlipSnapshotReader snapshotReader) : ITransientDependency
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
		if (snapshot == null)
		{
			throw new BusinessException(HRErrorCodes.PayrollSnapshotRequiredForSalarySlip)
				.WithData("employeeId", salarySlip.EmployeeId)
				.WithData("monthYear", salarySlip.MonthYear);
		}

		var snapshotEarnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
		var snapshotDeductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);

		return SalarySlipMatchesSnapshot(salarySlip, snapshot, snapshotEarnings, snapshotDeductions)
			? salarySlip
			: await salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
				snapshot,
				snapshotEarnings,
				snapshotDeductions,
				ParseDayCalculationMode(snapshot.DayCalculationMode));
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
