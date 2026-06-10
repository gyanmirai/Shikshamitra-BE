using System;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportDatabaseBackedCapabilities
{
	public static bool CanUseTable(HrReportRequestDto input) =>
		input.Groups is not { Count: > 0 } &&
		input.Summaries is not { Count: > 0 } &&
		input.Filters.Conditions is not { Count: > 0 } &&
		(input.Chart == null || input.Chart.Type == HrReportChartType.None);

	public static bool CanUseEmployeeGroup(HrReportRequestDto input)
	{
		var group = input.Groups?.SingleOrDefault();
		if (string.IsNullOrWhiteSpace(group) ||
			!HrReportCatalog.IsGroupable(HrReportDataSources.Employee, group) ||
			input.Filters.Conditions is { Count: > 0 })
		{
			return false;
		}

		if (input.Summaries is { Count: > 0 } && input.Summaries.Any(x => x.Function != HrReportSummaryFunction.Count))
		{
			return false;
		}

		return group is "department" or "designation" or "status" or "gender";
	}

	public static bool CanUsePayrollGroup(HrReportRequestDto input)
	{
		var group = input.Groups?.SingleOrDefault();
		if (string.IsNullOrWhiteSpace(group) ||
			!HrReportCatalog.IsGroupable(HrReportDataSources.Payroll, group) ||
			input.Filters.Conditions is { Count: > 0 })
		{
			return false;
		}

		var summaries = input.Summaries ?? [];
		return group is "month" or "department" or "designation" or "reviewStatus"
			&& summaries.All(summary =>
				HrReportCatalog.IsSummarizable(HrReportDataSources.Payroll, summary.Field) &&
				summary.Field is "basicSalary" or "grossSalary" or "totalDeduction" or "netPay" or "payableDays" &&
				summary.Function is HrReportSummaryFunction.Count or HrReportSummaryFunction.Sum or HrReportSummaryFunction.Average);
	}

	public static bool CanUseAttendanceGroup(HrReportRequestDto input)
	{
		var group = input.Groups?.SingleOrDefault();
		if (string.IsNullOrWhiteSpace(group) ||
			!HrReportCatalog.IsGroupable(HrReportDataSources.Attendance, group) ||
			input.Filters.Conditions is { Count: > 0 })
		{
			return false;
		}

		var summaries = input.Summaries ?? [];
		return group is "department" or "status"
			&& summaries.All(summary =>
				HrReportCatalog.IsSummarizable(HrReportDataSources.Attendance, summary.Field) &&
				summary.Field == "hoursWorked" &&
				summary.Function == HrReportSummaryFunction.Count);
	}

	public static bool CanUseLeaveGroup(HrReportRequestDto input)
	{
		var group = input.Groups?.SingleOrDefault();
		if (string.IsNullOrWhiteSpace(group) ||
			!HrReportCatalog.IsGroupable(HrReportDataSources.Leave, group) ||
			input.Filters.Conditions is { Count: > 0 })
		{
			return false;
		}

		var summaries = input.Summaries ?? [];
		return group is "employeeCode" or "leaveType" or "status"
			&& summaries.All(summary =>
				HrReportCatalog.IsSummarizable(HrReportDataSources.Leave, summary.Field) &&
				summary.Field == "approvedUnits" &&
				summary.Function is HrReportSummaryFunction.Count or HrReportSummaryFunction.Sum or HrReportSummaryFunction.Average);
	}

	public static bool CanUsePerformanceGroup(HrReportRequestDto input)
	{
		var group = input.Groups?.SingleOrDefault();
		if (string.IsNullOrWhiteSpace(group) ||
			!HrReportCatalog.IsGroupable(HrReportDataSources.Performance, group) ||
			input.Filters.Conditions is { Count: > 0 })
		{
			return false;
		}

		var summaries = input.Summaries ?? [];
		return group is "cycle" or "employeeCode" or "department" or "status" or "finalRating"
			&& summaries.All(summary =>
				HrReportCatalog.IsSummarizable(HrReportDataSources.Performance, summary.Field) &&
				summary.Field == "finalScore" &&
				summary.Function is HrReportSummaryFunction.Count or HrReportSummaryFunction.Sum or HrReportSummaryFunction.Average);
	}

	public static bool CanUsePolicyGroup(HrReportRequestDto input)
	{
		var group = input.Groups?.SingleOrDefault();
		if (string.IsNullOrWhiteSpace(group) ||
			!HrReportCatalog.IsGroupable(HrReportDataSources.Policies, group) ||
			input.Filters.Conditions is { Count: > 0 })
		{
			return false;
		}

		return (group is "recordType" or "action" or "isActive") && input.Summaries is not { Count: > 0 };
	}
}
