using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipSnapshotMatcher;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipSnapshotGenerationService(
	SalarySlipManager salarySlipManager,
	SalarySlipSnapshotReader snapshotReader) : ITransientDependency
{
	public async Task<SalarySlip> GenerateOrGetFromSnapshotAsync(Guid employeeId, string monthYear)
	{
		var normalizedMonthYear = NormalizeMonthYear(monthYear);
		var snapshot = await snapshotReader.GetAsync(employeeId, normalizedMonthYear);
		if (snapshot == null)
		{
			throw new BusinessException(HRErrorCodes.PayrollSnapshotRequiredForSalarySlip)
				.WithData("employeeId", employeeId)
				.WithData("monthYear", normalizedMonthYear);
		}

		var earnings = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EarningComponentsJson);
		var deductions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.DeductionComponentsJson);
		PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, earnings, deductions);
		return await salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
			snapshot,
			earnings,
			deductions,
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
