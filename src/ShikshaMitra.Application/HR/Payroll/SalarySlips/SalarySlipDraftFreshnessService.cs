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
	public async Task<SalarySlip> EnsureFreshAsync(SalarySlip salarySlip)
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
		var snapshotEmployerContributions = PayrollSnapshotComponentParser.BuildSalarySlipLineItems(snapshot.EmployerContributionsJson);
		PayrollSnapshotTotalGuard.EnsureMatchesLineItems(snapshot, snapshotEarnings, snapshotDeductions);

		return SalarySlipMatchesSnapshot(salarySlip, snapshot, snapshotEarnings, snapshotDeductions, snapshotEmployerContributions)
			? salarySlip
			: await salarySlipManager.GenerateOrGetSalarySlipFromSnapshotAsync(
				snapshot,
				snapshotEarnings,
				snapshotDeductions,
				snapshotEmployerContributions,
				ParseDayCalculationMode(snapshot.DayCalculationMode));
	}
}
