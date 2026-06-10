using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
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
public class HrReportPerformanceRowReader(
	IRepository<PerformanceReviewAssignment, Guid> performanceAssignmentRepository,
	IRepository<ProfessionalReviewCycle, Guid> reviewCycleRepository,
	HrReportEmployeeQueryService employeeQueryService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> BuildRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var query = await BuildQueryAsync(input, scope);
		var rows = await asyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.Cycle)
				.ThenBy(x => x.EmployeeCode)
				.Take(HrReportRequestProcessor.MaxExportRows));

		return rows.Select(ToRow).ToList();
	}

	public async Task<HrReportRowsResult> BuildRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var query = await BuildQueryAsync(input, scope);
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
		var groupField = input.Groups?.SingleOrDefault() ?? "department";
		var query = await BuildQueryAsync(input, scope);
		if (groupField == "status")
		{
			var statusGroupedQuery = query.GroupBy(x => x.Status).Select(group => new
			{
				Status = group.Key,
				RowCount = group.Count(),
				SumFinalScore = group.Sum(x => x.FinalScore),
				AvgFinalScore = group.Average(x => x.FinalScore)
			});
			var statusTotalCount = await asyncExecuter.CountAsync(statusGroupedQuery);
			var statusGroups = await asyncExecuter.ToListAsync(statusGroupedQuery);
			var statusRows = statusGroups.Select(group =>
			{
				var row = Row(
					(groupField, group.Status.ToString()),
					("rowCount", group.RowCount.ToString(CultureInfo.InvariantCulture)));
				HrReportAggregateRows.AddSummaryValues(row, input.Summaries, new Dictionary<string, string?>
				{
					["sum_finalScore"] = FormatDecimal(group.SumFinalScore),
					["avg_finalScore"] = FormatDecimal(group.AvgFinalScore),
					["count_finalScore"] = group.RowCount.ToString(CultureInfo.InvariantCulture)
				});
				return row;
			}).ToList();
			return HrReportAggregateRows.BuildResult(statusRows, input, exportAllRows, statusTotalCount, groupField);
		}
		var keyedQuery = groupField switch
		{
			"cycle" => query.Select(x => new { Label = x.Cycle, x.FinalScore }),
			"employeeCode" => query.Select(x => new { Label = x.EmployeeCode, x.FinalScore }),
			"finalRating" => query.Select(x => new { Label = x.FinalRating ?? string.Empty, x.FinalScore }),
			_ => query.Select(x => new { Label = x.Department ?? string.Empty, x.FinalScore })
		};
		var groupedQuery = keyedQuery.GroupBy(x => x.Label).Select(group => new
		{
			Label = group.Key,
			RowCount = group.Count(),
			SumFinalScore = group.Sum(x => x.FinalScore),
			AvgFinalScore = group.Average(x => x.FinalScore)
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
				["sum_finalScore"] = FormatDecimal(group.SumFinalScore),
				["avg_finalScore"] = FormatDecimal(group.AvgFinalScore),
				["count_finalScore"] = group.RowCount.ToString(CultureInfo.InvariantCulture)
			});
			return row;
		}).ToList();
		return HrReportAggregateRows.BuildResult(aggregateRows, input, exportAllRows, totalCount, groupField);
	}

	private async Task<IQueryable<PerformanceReportRow>> BuildQueryAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var assignmentQuery = await performanceAssignmentRepository.GetQueryableAsync();
		var cycleQuery = await reviewCycleRepository.GetQueryableAsync();
		if (input.Filters.FromDate.HasValue)
		{
			var from = input.Filters.FromDate.Value.ToDateTime(TimeOnly.MinValue);
			assignmentQuery = assignmentQuery.Where(x => x.FinalizedAt.HasValue && x.FinalizedAt.Value >= from);
		}
		if (input.Filters.ToDate.HasValue)
		{
			var to = input.Filters.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
			assignmentQuery = assignmentQuery.Where(x => x.FinalizedAt.HasValue && x.FinalizedAt.Value <= to);
		}
		return
			from assignment in assignmentQuery
			join employee in employeeQuery on assignment.EmployeeId equals employee.Id
			join cycle in cycleQuery on assignment.CycleId equals cycle.Id
			select new PerformanceReportRow
			{
				Cycle = cycle.Name,
				EmployeeCode = employee.EmployeeCode,
				EmployeeName = employee.EmployeeName,
				FirstName = employee.FirstName,
				LastName = employee.LastName,
				Department = employee.Department,
				Status = assignment.Status,
				FinalScore = assignment.FinalScore,
				FinalRating = assignment.FinalRating,
				FinalizedAt = assignment.FinalizedAt
			};
	}

	private static Dictionary<string, string?> ToRow(PerformanceReportRow r) =>
		Row(
			("cycle", r.Cycle),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("finalScore", FormatDecimal(r.FinalScore)),
			("finalRating", r.FinalRating),
			("finalizedAt", FormatDateTime(r.FinalizedAt)));

	private static IQueryable<T> ApplySort<T>(IQueryable<T> query, List<HrReportSortDto>? sorts)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return query.OrderBy("Cycle ascending, EmployeeCode ascending");
		}

		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		return firstSort.Field switch
		{
			"employeeCode" => OrderByProperty(query, "EmployeeCode", desc),
			"employeeName" => desc ? query.OrderBy("FirstName descending, LastName descending") : query.OrderBy("FirstName ascending, LastName ascending"),
			"department" => OrderByProperty(query, "Department", desc),
			"status" => OrderByProperty(query, "Status", desc),
			"finalScore" => OrderByProperty(query, "FinalScore", desc),
			"finalRating" => OrderByProperty(query, "FinalRating", desc),
			"finalizedAt" => OrderByProperty(query, "FinalizedAt", desc),
			_ => OrderByProperty(query, "Cycle", desc)
		};
	}

	private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyName, bool descending) =>
		query.OrderBy($"{propertyName} {(descending ? "descending" : "ascending")}");
	private sealed class PerformanceReportRow
	{
		public string Cycle { get; init; } = string.Empty;
		public string EmployeeCode { get; init; } = string.Empty;
		public string EmployeeName { get; init; } = string.Empty;
		public string? FirstName { get; init; }
		public string? LastName { get; init; }
		public string? Department { get; init; }
		public ProfessionalReviewStatus Status { get; init; }
		public decimal? FinalScore { get; init; }
		public string? FinalRating { get; init; }
		public DateTime? FinalizedAt { get; init; }
	}
}
