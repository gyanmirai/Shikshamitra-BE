using System.Collections.Generic;
using System.Globalization;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

internal static class HrReportPayrollAggregateRows
{
	public static Dictionary<string, string?> Build(
		string groupField,
		string? label,
		int rowCount,
		decimal sumBasicSalary,
		decimal avgBasicSalary,
		decimal sumGrossSalary,
		decimal avgGrossSalary,
		decimal sumTotalDeduction,
		decimal avgTotalDeduction,
		decimal sumNetPay,
		decimal avgNetPay,
		decimal sumPayableDays,
		decimal avgPayableDays,
		List<HrReportSummaryDto>? summaries)
	{
		var row = Row(
			(groupField, string.IsNullOrWhiteSpace(label) ? "Unspecified" : label),
			("rowCount", rowCount.ToString(CultureInfo.InvariantCulture)));
		HrReportAggregateRows.AddSummaryValues(row, summaries, new Dictionary<string, string?>
		{
			["sum_basicSalary"] = FormatMoney(sumBasicSalary),
			["avg_basicSalary"] = FormatMoney(avgBasicSalary),
			["count_basicSalary"] = rowCount.ToString(CultureInfo.InvariantCulture),
			["sum_grossSalary"] = FormatMoney(sumGrossSalary),
			["avg_grossSalary"] = FormatMoney(avgGrossSalary),
			["count_grossSalary"] = rowCount.ToString(CultureInfo.InvariantCulture),
			["sum_totalDeduction"] = FormatMoney(sumTotalDeduction),
			["avg_totalDeduction"] = FormatMoney(avgTotalDeduction),
			["count_totalDeduction"] = rowCount.ToString(CultureInfo.InvariantCulture),
			["sum_netPay"] = FormatMoney(sumNetPay),
			["avg_netPay"] = FormatMoney(avgNetPay),
			["count_netPay"] = rowCount.ToString(CultureInfo.InvariantCulture),
			["sum_payableDays"] = FormatDecimal(sumPayableDays),
			["avg_payableDays"] = FormatDecimal(avgPayableDays),
			["count_payableDays"] = rowCount.ToString(CultureInfo.InvariantCulture)
		});
		return row;
	}
}
