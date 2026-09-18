using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public static class PayrollSnapshotTotalGuard
{
	public static void EnsureMatchesLineItems(
		PayrollRunItemSnapshot snapshot,
		IReadOnlyList<SalarySlipLineItem> earnings,
		IReadOnlyList<SalarySlipLineItem> deductions)
	{
		var totalEarnings = Math.Round(earnings.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
		var totalDeductions = Math.Round(deductions.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
		var netPay = Math.Round(totalEarnings - totalDeductions, 2, MidpointRounding.AwayFromZero);
		if (Math.Round(earnings.Where(x => !x.IsTemp).Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero) != Math.Round(snapshot.GrossSalary, 2, MidpointRounding.AwayFromZero) ||
		    totalDeductions != Math.Round(snapshot.TotalDeduction, 2, MidpointRounding.AwayFromZero) ||
		    netPay != Math.Round(snapshot.NetPay, 2, MidpointRounding.AwayFromZero))
		{
			throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
				.WithData("employeeId", snapshot.EmployeeId)
				.WithData("month", snapshot.MonthYear)
				.WithData("reason", "Reviewed payroll snapshot totals do not match snapshot line items.");
		}
	}
}
