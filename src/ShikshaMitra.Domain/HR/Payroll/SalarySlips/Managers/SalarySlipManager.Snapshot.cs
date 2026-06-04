using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public partial class SalarySlipManager
{
	public async Task<SalarySlip> GenerateOrGetSalarySlipFromSnapshotAsync(
		PayrollRunItemSnapshot snapshot,
		List<SalarySlipLineItem> earningLineItems,
		List<SalarySlipLineItem> deductionLineItems,
		DayCalculationType dayCalculationMode,
		CancellationToken cancellationToken = default)
	{
		return await _snapshotWriter.GenerateOrGetSalarySlipFromSnapshotAsync(
			snapshot,
			earningLineItems,
			deductionLineItems,
			dayCalculationMode,
			cancellationToken);
	}

	public void ValidateMonthYearFormat(string monthYear)
	{
		SalarySlipGenerationRules.NormalizeMonthYear(monthYear);
	}
}
