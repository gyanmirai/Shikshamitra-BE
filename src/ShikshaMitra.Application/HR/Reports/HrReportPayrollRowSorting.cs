using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportPayrollRowSorting
{
	public static IQueryable<T> Apply<T>(IQueryable<T> query, List<HrReportSortDto>? sorts)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return OrderByProperty(query, "EmployeeCode", false);
		}

		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		return firstSort.Field switch
		{
			"month" => OrderByProperty(query, "Month", desc),
			"employeeName" => OrderByProperty(query, "EmployeeName", desc),
			"department" => OrderByProperty(query, "Department", desc),
			"designation" => OrderByProperty(query, "Designation", desc),
			"basicSalary" => OrderByProperty(query, "BasicSalary", desc),
			"grossSalary" => OrderByProperty(query, "GrossSalary", desc),
			"totalDeduction" => OrderByProperty(query, "TotalDeduction", desc),
			"netPay" => OrderByProperty(query, "NetPay", desc),
			"payableDays" => OrderByProperty(query, "PayableDays", desc),
			"reviewStatus" => OrderByProperty(query, "ReviewStatus", desc),
			"isLocked" => OrderByProperty(query, "IsLocked", desc),
			_ => OrderByProperty(query, "EmployeeCode", desc)
		};
	}

	private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyName, bool descending) =>
		query.OrderBy($"{propertyName} {(descending ? "descending" : "ascending")}");
}
