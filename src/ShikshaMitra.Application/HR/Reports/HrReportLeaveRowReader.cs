using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
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

public class HrReportLeaveRowReader(
	IRepository<EmployeeLeave, Guid> leaveRepository,
	HrReportEmployeeQueryService employeeQueryService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> BuildRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var leaveQuery = HrReportDateFilter.ApplyDateOnlyRange(await leaveRepository.GetQueryableAsync(), input.Filters, x => x.StartDateOnly);
		var rows = await asyncExecuter.ToListAsync((
			from leave in leaveQuery
			join employee in employeeQuery on leave.EmployeeId equals employee.Id
			select new LeaveReportRow
			{
				PublicId = leave.PublicId,
				EmployeeCode = employee.EmployeeCode,
				EmployeeName = employee.EmployeeName,
				FirstName = employee.FirstName,
				LastName = employee.LastName,
				LeaveType = leave.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
				StartDateOnly = leave.StartDateOnly,
				EndDateOnly = leave.EndDateOnly,
				Status = leave.Status,
				ApprovedLeaveUnits = leave.ApprovedLeaveUnits,
				Description = leave.Description
			}).OrderByDescending(x => x.StartDateOnly).ThenBy(x => x.EmployeeCode).Take(HrReportRequestProcessor.MaxExportRows));

		return rows.Select(ToRow).ToList();
	}

	public async Task<HrReportRowsResult> BuildRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var leaveQuery = HrReportDateFilter.ApplyDateOnlyRange(await leaveRepository.GetQueryableAsync(), input.Filters, x => x.StartDateOnly);
		var query =
			from leave in leaveQuery
			join employee in employeeQuery on leave.EmployeeId equals employee.Id
			select new LeaveReportRow
			{
				PublicId = leave.PublicId,
				EmployeeCode = employee.EmployeeCode,
				EmployeeName = employee.EmployeeName,
				FirstName = employee.FirstName,
				LastName = employee.LastName,
				LeaveType = leave.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
				StartDateOnly = leave.StartDateOnly,
				EndDateOnly = leave.EndDateOnly,
				Status = leave.Status,
				ApprovedLeaveUnits = leave.ApprovedLeaveUnits,
				Description = leave.Description
			};

		var totalCount = await asyncExecuter.CountAsync(query);
		var sortedQuery = ApplySort(query, input.Sorts);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var rows = await asyncExecuter.ToListAsync(sortedQuery.Skip(skip).Take(take));

		return new HrReportRowsResult(rows.Select(ToRow).ToList(), totalCount, true);
	}

	public async Task<HrReportRowsResult> BuildGroupedRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var groupField = input.Groups?.SingleOrDefault() ?? "status";
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var leaveQuery = HrReportDateFilter.ApplyDateOnlyRange(await leaveRepository.GetQueryableAsync(), input.Filters, x => x.StartDateOnly);
		var query =
			from leave in leaveQuery
			join employee in employeeQuery on leave.EmployeeId equals employee.Id
			select new
			{
				employee.EmployeeCode,
				LeaveType = leave.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
				Status = leave.Status,
				leave.ApprovedLeaveUnits
			};

		var keyedQuery = groupField switch
		{
			"employeeCode" => query.Select(x => new { Label = x.EmployeeCode, x.ApprovedLeaveUnits }),
			"leaveType" => query.Select(x => new { Label = x.LeaveType, x.ApprovedLeaveUnits }),
			_ => query.Select(x => new { Label = x.Status.ToString(), x.ApprovedLeaveUnits })
		};

		var groupedQuery = keyedQuery.GroupBy(x => x.Label).Select(group => new
		{
			Label = group.Key,
			RowCount = group.Count(),
			SumApprovedUnits = group.Sum(x => x.ApprovedLeaveUnits),
			AvgApprovedUnits = group.Average(x => x.ApprovedLeaveUnits)
		});

		var totalCount = await asyncExecuter.CountAsync(groupedQuery);
		var groups = await asyncExecuter.ToListAsync(groupedQuery);
		var aggregateRows = groups.Select(group =>
		{
			var row = Row(
				(groupField, string.IsNullOrWhiteSpace(group.Label) ? "Unspecified" : group.Label),
				("rowCount", group.RowCount.ToString(CultureInfo.InvariantCulture)));
			HrReportAggregateRows.AddSummaryValues(row, input.Summaries, new Dictionary<string, string?>
			{
				["sum_approvedUnits"] = FormatDecimal(group.SumApprovedUnits),
				["avg_approvedUnits"] = FormatDecimal(group.AvgApprovedUnits),
				["count_approvedUnits"] = group.RowCount.ToString(CultureInfo.InvariantCulture)
			});
			return row;
		}).ToList();
		return HrReportAggregateRows.BuildResult(aggregateRows, input, exportAllRows, totalCount, groupField);
	}

	private static Dictionary<string, string?> ToRow(LeaveReportRow r) =>
		Row(
			("requestNo", r.PublicId.ToString(CultureInfo.InvariantCulture)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("leaveType", r.LeaveType),
			("startDate", FormatDate(r.StartDateOnly)),
			("endDate", FormatDate(r.EndDateOnly)),
			("status", r.Status.ToString()),
			("approvedUnits", FormatDecimal(r.ApprovedLeaveUnits)),
			("description", r.Description));

	private static IQueryable<T> ApplySort<T>(IQueryable<T> query, List<HrReportSortDto>? sorts)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return query.OrderBy("StartDateOnly descending, EmployeeCode ascending");
		}

		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		return firstSort.Field switch
		{
			"requestNo" => OrderByProperty(query, "PublicId", desc),
			"employeeCode" => OrderByProperty(query, "EmployeeCode", desc),
			"employeeName" => desc ? query.OrderBy("FirstName descending, LastName descending") : query.OrderBy("FirstName ascending, LastName ascending"),
			"leaveType" => OrderByProperty(query, "LeaveType", desc),
			"endDate" => OrderByProperty(query, "EndDateOnly", desc),
			"status" => OrderByProperty(query, "Status", desc),
			"approvedUnits" => OrderByProperty(query, "ApprovedLeaveUnits", desc),
			_ => OrderByProperty(query, "StartDateOnly", desc)
		};
	}

	private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyName, bool descending) =>
		query.OrderBy($"{propertyName} {(descending ? "descending" : "ascending")}");

	private sealed class LeaveReportRow
	{
		public long PublicId { get; init; }
		public string EmployeeCode { get; init; } = string.Empty;
		public string EmployeeName { get; init; } = string.Empty;
		public string? FirstName { get; init; }
		public string? LastName { get; init; }
		public string LeaveType { get; init; } = string.Empty;
		public DateOnly StartDateOnly { get; init; }
		public DateOnly EndDateOnly { get; init; }
		public EmployeeLeaveStatusType Status { get; init; }
		public decimal ApprovedLeaveUnits { get; init; }
		public string? Description { get; init; }
	}
}
