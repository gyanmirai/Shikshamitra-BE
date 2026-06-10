using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

public class HrReportEmployeePayrollRowReader(
	IRepository<PayrollRunItemSnapshot, Guid> payrollSnapshotRepository,
	HrReportEmployeeQueryService employeeQueryService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> BuildPayrollRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var query = await BuildPayrollQueryAsync(input, scope);
		var rows = await asyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.EmployeeCode)
				.Take(HrReportRequestProcessor.MaxExportRows));
		return rows.Select(ToRow).ToList();
	}

	public async Task<HrReportRowsResult> BuildPayrollRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var query = await BuildPayrollQueryAsync(input, scope);
		var totalCount = await asyncExecuter.CountAsync(query);
		var sortedQuery = HrReportPayrollRowSorting.Apply(query, input.Sorts);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var rows = await asyncExecuter.ToListAsync(sortedQuery.Skip(skip).Take(take));

		return new HrReportRowsResult(rows.Select(ToRow).ToList(), totalCount, true);
	}

	public async Task<HrReportRowsResult> BuildPayrollGroupedRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var groupField = input.Groups?.SingleOrDefault() ?? "department";
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var snapshotQuery = await BuildMonthFilteredSnapshotQueryAsync(input);
		var query =
			from snapshot in snapshotQuery
			join employee in employeeQuery on snapshot.EmployeeId equals employee.Id
			select new
			{
				Month = snapshot.MonthYear,
				employee.Department,
				employee.Designation,
				snapshot.ReviewStatus,
				snapshot.BasicSalary,
				snapshot.GrossSalary,
				snapshot.TotalDeduction,
				snapshot.NetPay,
				snapshot.PayableDays
			};

		var keyedQuery = groupField switch
		{
			"month" => query.Select(x => new { Label = x.Month, x.BasicSalary, x.GrossSalary, x.TotalDeduction, x.NetPay, x.PayableDays }),
			"designation" => query.Select(x => new { Label = x.Designation ?? string.Empty, x.BasicSalary, x.GrossSalary, x.TotalDeduction, x.NetPay, x.PayableDays }),
			"reviewStatus" => query.Select(x => new { Label = x.ReviewStatus ?? string.Empty, x.BasicSalary, x.GrossSalary, x.TotalDeduction, x.NetPay, x.PayableDays }),
			_ => query.Select(x => new { Label = x.Department ?? string.Empty, x.BasicSalary, x.GrossSalary, x.TotalDeduction, x.NetPay, x.PayableDays })
		};

		var groupedQuery = keyedQuery.GroupBy(x => x.Label).Select(group => new
		{
			Label = group.Key,
			RowCount = group.Count(),
			SumBasicSalary = group.Sum(x => x.BasicSalary),
			AvgBasicSalary = group.Average(x => x.BasicSalary),
			SumGrossSalary = group.Sum(x => x.GrossSalary),
			AvgGrossSalary = group.Average(x => x.GrossSalary),
			SumTotalDeduction = group.Sum(x => x.TotalDeduction),
			AvgTotalDeduction = group.Average(x => x.TotalDeduction),
			SumNetPay = group.Sum(x => x.NetPay),
			AvgNetPay = group.Average(x => x.NetPay),
			SumPayableDays = group.Sum(x => x.PayableDays),
			AvgPayableDays = group.Average(x => x.PayableDays)
		});

		var totalCount = await asyncExecuter.CountAsync(groupedQuery);
		var groups = await asyncExecuter.ToListAsync(groupedQuery);
		var aggregateRows = groups.Select(group => HrReportPayrollAggregateRows.Build(
			groupField,
			group.Label,
			group.RowCount,
			group.SumBasicSalary,
			group.AvgBasicSalary,
			group.SumGrossSalary,
			group.AvgGrossSalary,
			group.SumTotalDeduction,
			group.AvgTotalDeduction,
			group.SumNetPay,
			group.AvgNetPay,
			group.SumPayableDays,
			group.AvgPayableDays,
			input.Summaries)).ToList();

		return HrReportAggregateRows.BuildResult(aggregateRows, input, exportAllRows, totalCount, groupField);
	}

	private async Task<IQueryable<PayrollReportRow>> BuildPayrollQueryAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var snapshotQuery = await BuildMonthFilteredSnapshotQueryAsync(input);
		return
			from snapshot in snapshotQuery
			join employee in employeeQuery on snapshot.EmployeeId equals employee.Id
			select new PayrollReportRow
			{
				Month = snapshot.MonthYear,
				EmployeeCode = snapshot.EmployeeIdentifier,
				EmployeeName = snapshot.EmployeeName,
				Department = employee.Department,
				Designation = employee.Designation,
				BasicSalary = snapshot.BasicSalary,
				GrossSalary = snapshot.GrossSalary,
				TotalDeduction = snapshot.TotalDeduction,
				NetPay = snapshot.NetPay,
				PayableDays = snapshot.PayableDays,
				ReviewStatus = snapshot.ReviewStatus,
				IsLocked = snapshot.IsLocked
			};
	}

	private async Task<IQueryable<PayrollRunItemSnapshot>> BuildMonthFilteredSnapshotQueryAsync(HrReportRequestDto input)
	{
		var snapshotQuery = await payrollSnapshotRepository.GetQueryableAsync();
		if (PayrollMonthNormalizer.TryNormalizeToInternal(input.Filters.Month, out var month))
		{
			snapshotQuery = snapshotQuery.Where(x => x.MonthYear == month);
		}

		return snapshotQuery;
	}

	private static Dictionary<string, string?> ToRow(PayrollReportRow r) =>
		Row(
			("month", r.Month),
			("employeeCode", r.EmployeeCode),
			("employeeName", r.EmployeeName),
			("department", r.Department),
			("designation", r.Designation),
			("basicSalary", FormatMoney(r.BasicSalary)),
			("grossSalary", FormatMoney(r.GrossSalary)),
			("totalDeduction", FormatMoney(r.TotalDeduction)),
			("netPay", FormatMoney(r.NetPay)),
			("payableDays", FormatDecimal(r.PayableDays)),
			("reviewStatus", r.ReviewStatus),
			("isLocked", r.IsLocked ? "Yes" : "No"));

	private sealed class PayrollReportRow
	{
		public string Month { get; init; } = string.Empty;
		public string EmployeeCode { get; init; } = string.Empty;
		public string EmployeeName { get; init; } = string.Empty;
		public string? Department { get; init; }
		public string? Designation { get; init; }
		public decimal BasicSalary { get; init; }
		public decimal GrossSalary { get; init; }
		public decimal TotalDeduction { get; init; }
		public decimal NetPay { get; init; }
		public decimal PayableDays { get; init; }
		public string? ReviewStatus { get; init; }
		public bool IsLocked { get; init; }
	}
}
