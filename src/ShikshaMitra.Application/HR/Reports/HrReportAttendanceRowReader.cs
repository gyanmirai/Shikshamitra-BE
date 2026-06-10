using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

public class HrReportAttendanceRowReader(
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	HrReportEmployeeQueryService employeeQueryService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HrReportRowsResult> BuildRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var attendanceQuery = HrReportDateFilter.ApplyDateOnlyRange(await attendanceRepository.GetQueryableAsync(), input.Filters, x => x.AttendanceDate);
		var query =
			from attendance in attendanceQuery
			join employee in employeeQuery on attendance.EmployeeId equals employee.Id
			select new
			{
				attendance.AttendanceDate,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				employee.Department,
				Status = attendance.StatusType,
				attendance.TimeIn,
				attendance.TimeOut,
				attendance.TotalHoursWorked,
				attendance.Remarks
			};

		var totalCount = await asyncExecuter.CountAsync(query);
		var sortedQuery = ApplySort(query, input.Sorts);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var rows = await asyncExecuter.ToListAsync(sortedQuery.Skip(skip).Take(take));

		return new HrReportRowsResult(rows.Select(r => Row(
			("attendanceDate", FormatDate(r.AttendanceDate)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("timeIn", FormatDateTime(r.TimeIn)),
			("timeOut", FormatDateTime(r.TimeOut)),
			("hoursWorked", r.TotalHoursWorked?.ToString("0.##", CultureInfo.InvariantCulture)),
			("remarks", r.Remarks))).ToList(), totalCount, true);
	}

	public async Task<HrReportRowsResult> BuildGroupedRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var groupField = input.Groups?.SingleOrDefault() ?? "status";
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var attendanceQuery = HrReportDateFilter.ApplyDateOnlyRange(await attendanceRepository.GetQueryableAsync(), input.Filters, x => x.AttendanceDate);
		var query =
			from attendance in attendanceQuery
			join employee in employeeQuery on attendance.EmployeeId equals employee.Id
			select new
			{
				employee.Department,
				Status = attendance.StatusType,
				attendance.TotalHoursWorked
			};

		if (groupField == "department")
		{
			var groupedQuery = query
				.GroupBy(x => x.Department ?? string.Empty)
				.Select(group => new
				{
					Label = group.Key,
					RowCount = group.Count()
				});
			var totalCount = await asyncExecuter.CountAsync(groupedQuery);
			var groups = await asyncExecuter.ToListAsync(groupedQuery);
			var aggregateRows = groups.Select(group => BuildAggregateRow(
				groupField,
				group.Label,
				group.RowCount,
				input.Summaries)).ToList();
			return HrReportAggregateRows.BuildResult(aggregateRows, input, exportAllRows, totalCount, groupField);
		}
		else
		{
			var groupedQuery = query
				.GroupBy(x => x.Status)
				.Select(group => new
				{
					Status = group.Key,
					RowCount = group.Count()
				});
			var totalCount = await asyncExecuter.CountAsync(groupedQuery);
			var groups = await asyncExecuter.ToListAsync(groupedQuery);
			var aggregateRows = groups.Select(group => BuildAggregateRow(
				groupField,
				group.Status.ToString(),
				group.RowCount,
				input.Summaries)).ToList();
			return HrReportAggregateRows.BuildResult(aggregateRows, input, exportAllRows, totalCount, groupField);
		}
	}

	public async Task<List<Dictionary<string, string?>>> BuildRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var attendanceQuery = HrReportDateFilter.ApplyDateOnlyRange(await attendanceRepository.GetQueryableAsync(), input.Filters, x => x.AttendanceDate);
		var rows = await asyncExecuter.ToListAsync((
			from attendance in attendanceQuery
			join employee in employeeQuery on attendance.EmployeeId equals employee.Id
			select new
			{
				attendance.AttendanceDate,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				employee.Department,
				Status = attendance.StatusType,
				attendance.TimeIn,
				attendance.TimeOut,
				attendance.TotalHoursWorked,
				attendance.Remarks
			}).OrderByDescending(x => x.AttendanceDate).ThenBy(x => x.EmployeeCode).Take(HrReportRequestProcessor.MaxExportRows));

		return rows.Select(r => Row(
			("attendanceDate", FormatDate(r.AttendanceDate)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("timeIn", FormatDateTime(r.TimeIn)),
			("timeOut", FormatDateTime(r.TimeOut)),
			("hoursWorked", r.TotalHoursWorked?.ToString("0.##", CultureInfo.InvariantCulture)),
			("remarks", r.Remarks))).ToList();
	}

	private static Dictionary<string, string?> BuildAggregateRow(
		string groupField,
		string? label,
		int rowCount,
		List<HrReportSummaryDto>? summaries)
	{
		var row = Row(
			(groupField, string.IsNullOrWhiteSpace(label) ? "Unspecified" : label),
			("rowCount", rowCount.ToString(CultureInfo.InvariantCulture)));
		HrReportAggregateRows.AddSummaryValues(row, summaries, new Dictionary<string, string?>
		{
			["count_hoursWorked"] = rowCount.ToString(CultureInfo.InvariantCulture)
		});
		return row;
	}

	private static IQueryable<T> ApplySort<T>(IQueryable<T> query, List<HrReportSortDto>? sorts)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return query.OrderBy("AttendanceDate descending, EmployeeCode ascending");
		}

		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		return firstSort.Field switch
		{
			"employeeCode" => OrderByProperty(query, "EmployeeCode", desc),
			"employeeName" => desc ? query.OrderBy("FirstName descending, LastName descending") : query.OrderBy("FirstName ascending, LastName ascending"),
			"department" => desc ? query.OrderBy("Department descending, EmployeeCode ascending") : query.OrderBy("Department ascending, EmployeeCode ascending"),
			"status" => OrderByProperty(query, "Status", desc),
			"hoursWorked" => OrderByProperty(query, "TotalHoursWorked", desc),
			_ => desc ? query.OrderBy("AttendanceDate descending, EmployeeCode ascending") : query.OrderBy("AttendanceDate ascending, EmployeeCode ascending")
		};
	}

	private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyName, bool descending) =>
		query.OrderBy($"{propertyName} {(descending ? "descending" : "ascending")}");
}
