using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

public class HrReportEmployeeRowReader(
	HrReportEmployeeQueryService employeeQueryService,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HrReportRowsResult> BuildRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var query = await BuildQueryAsync(input, scope, true);
		var totalCount = await asyncExecuter.CountAsync(query);
		query = HrReportEmployeeQueryFilters.ApplySorts(query, input.Sorts);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var employees = await asyncExecuter.ToListAsync(query.Skip(skip).Take(take).Select(ToReportRow()));
		return new HrReportRowsResult(ToRows(employees), totalCount, true);
	}

	public async Task<List<Dictionary<string, string?>>> BuildRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var query = await BuildQueryAsync(input, scope, false);
		var employees = await asyncExecuter.ToListAsync(
			query
				.OrderBy(e => e.EmployeeCode)
				.Select(ToReportRow())
				.Take(HrReportRequestProcessor.MaxExportRows));
		return ToRows(employees);
	}

	public async Task<HrReportRowsResult> BuildGroupedRowsResultAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var groupField = input.Groups?.SingleOrDefault() ?? "department";
		var query = await BuildQueryAsync(input, scope, false);
		var groupedQuery = groupField switch
		{
			"designation" => query
				.GroupBy(e => e.Designation ?? string.Empty)
				.Select(group => new { Label = group.Key, RowCount = group.Count() }),
			"status" => query
				.GroupBy(e => e.EmployeeStatusType)
				.Select(group => new { Label = group.Key.HasValue ? group.Key.Value.ToString() : string.Empty, RowCount = group.Count() }),
			"gender" => query
				.GroupBy(e => e.GenderType)
				.Select(group => new { Label = group.Key.HasValue ? group.Key.Value.ToString() : string.Empty, RowCount = group.Count() }),
			_ => query
				.GroupBy(e => e.Department ?? string.Empty)
				.Select(group => new { Label = group.Key, RowCount = group.Count() })
		};
		var totalCount = await asyncExecuter.CountAsync(groupedQuery);
		groupedQuery = HrReportEmployeeQueryFilters.ApplyGroupSorts(groupedQuery, input.Sorts, groupField);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var groups = await asyncExecuter.ToListAsync(groupedQuery.Skip(skip).Take(take));
		return new HrReportRowsResult(groups.Select(group => Row(
			(groupField, string.IsNullOrWhiteSpace(group.Label) ? "Unspecified" : group.Label),
			("rowCount", group.RowCount.ToString(CultureInfo.InvariantCulture)))).ToList(), totalCount, true);
	}

	private async Task<IQueryable<Employee>> BuildQueryAsync(HrReportRequestDto input, UserScopeContext scope, bool includeConditions)
	{
		var query = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		query = HrReportEmployeeQueryFilters.ApplyDateRange(query, input.Filters);
		return includeConditions ? HrReportEmployeeQueryFilters.ApplyConditionFilters(query, input.Filters.Conditions) : query;
	}

	private static Expression<Func<Employee, EmployeeReportRow>> ToReportRow() =>
		e => new EmployeeReportRow(
			e.EmployeeCode,
			e.EmployeeName,
			e.FirstName,
			e.LastName,
			e.Department,
			e.Designation,
			e.EmployeeStatusType,
			e.GenderType,
			e.MobileNo,
			e.Email,
			e.DateOfJoining,
			e.DateOfBirth);

	private static List<Dictionary<string, string?>> ToRows(IEnumerable<EmployeeReportRow> employees) =>
		employees.Select(employee => Row(
			("employeeCode", employee.EmployeeCode),
			("employeeName", DisplayName(employee.EmployeeName, employee.FirstName, employee.LastName)),
			("department", employee.Department),
			("designation", employee.Designation),
			("status", employee.EmployeeStatusType?.ToString()),
			("gender", employee.GenderType?.ToString()),
			("mobileNo", employee.MobileNo),
			("email", employee.Email),
			("dateOfJoining", FormatDate(employee.DateOfJoining)),
			("dateOfBirth", FormatDate(employee.DateOfBirth)))).ToList();

	private sealed record EmployeeReportRow(
		string EmployeeCode,
		string EmployeeName,
		string? FirstName,
		string? LastName,
		string? Department,
		string? Designation,
		EmployeeStatusType? EmployeeStatusType,
		GenderType? GenderType,
		string? MobileNo,
		string? Email,
		DateOnly? DateOfJoining,
		DateOnly? DateOfBirth);
}
