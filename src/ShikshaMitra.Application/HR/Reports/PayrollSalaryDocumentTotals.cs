using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class PayrollSalaryDocumentTotals
{
	private static readonly string[] Fields = ["salary", "grossSalary", "basicSalary", "pfSalary", "pf", "pt", "tds", "esic", "otherDeductions", "totalEarnings", "totalDeduction", "netPay", "casualLeaveDays", "paidLeaveDays", "unpaidLeaveDays", "payableDays"];
	public static Dictionary<string, decimal> Sum(IEnumerable<Dictionary<string, string?>> rows)
	{
		var available = rows.Where(x => x.GetValueOrDefault("status") != "Unavailable").ToList();
		return Fields.Where(field => available.Any(row => decimal.TryParse(row.GetValueOrDefault(field), NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
			.ToDictionary(field => field, field => available.Sum(row => decimal.TryParse(row.GetValueOrDefault(field), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m));
	}
}
