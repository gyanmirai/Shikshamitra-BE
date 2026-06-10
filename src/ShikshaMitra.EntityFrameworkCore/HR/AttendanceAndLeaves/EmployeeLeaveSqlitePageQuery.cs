using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class EmployeeLeaveSqlitePageQuery
{
	public static async Task<(int TotalCount, List<Guid> PageIds)> GetManagementListPageAsync(
		IQueryable<EmployeeLeave> dbSet,
		DateTime rs,
		DateTime re,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId,
		IReadOnlyCollection<Guid>? employeeIds,
		CancellationToken cancellationToken)
	{
		var rows = await ReadRowsAsync(dbSet, cancellationToken);
		var filtered = rows.Where(r =>
		{
			var date = r.StartDate.DateTime.Date;
			return r.AcademicYearId == filters.AcademicYearId && date >= rs && date <= re;
		});

		return PageRows(ApplyFilters(filtered, filters, search, employeeId, employeeIds), skip, take);
	}

	public static async Task<(int TotalCount, List<Guid> PageIds)> GetCalendarPageAsync(
		IQueryable<EmployeeLeave> dbSet,
		DateTimeOffset rs,
		DateTimeOffset re,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId,
		IReadOnlyCollection<Guid>? employeeIds,
		CancellationToken cancellationToken)
	{
		var rows = await ReadRowsAsync(dbSet, cancellationToken);
		var filtered = rows.Where(r =>
			r.AcademicYearId == filters.AcademicYearId &&
			r.StartDate <= re &&
			r.EndDate >= rs);

		return PageRows(ApplyFilters(filtered, filters, search, employeeId, employeeIds), skip, take);
	}

	private static async Task<List<EmployeeLeavePageRow>> ReadRowsAsync(
		IQueryable<EmployeeLeave> dbSet,
		CancellationToken cancellationToken)
	{
		return await dbSet
			.Select(x => new EmployeeLeavePageRow(
				x.Id,
				x.EmployeeId,
				x.StartDate,
				x.EndDate,
				x.CreationTime,
				x.Description,
				x.Status,
				x.LeavePolicyId,
				x.LeavePolicy != null ? x.LeavePolicy.AcademicYearId : null,
				x.LeaveTypeId,
				x.Employee != null ? x.Employee.EmployeeCode : string.Empty,
				x.Employee != null ? x.Employee.FirstName : null,
				x.Employee != null ? x.Employee.LastName : null,
				x.Employee != null ? x.Employee.MiddleName : null))
			.ToListAsync(cancellationToken);
	}

	private static IEnumerable<EmployeeLeavePageRow> ApplyFilters(
		IEnumerable<EmployeeLeavePageRow> rows,
		LeaveManagementListFilters filters,
		string? search,
		Guid? employeeId,
		IReadOnlyCollection<Guid>? employeeIds)
	{
		if (filters.Status.HasValue)
		{
			rows = rows.Where(r => r.Status == filters.Status.Value);
		}

		if (filters.LeavePolicyId.HasValue)
		{
			rows = rows.Where(r => r.LeavePolicyId == filters.LeavePolicyId.Value);
		}

		if (filters.LeaveTypeMasterId.HasValue)
		{
			rows = rows.Where(r => r.MasterLeaveTypeId == filters.LeaveTypeMasterId.Value);
		}

		if (employeeId.HasValue)
		{
			rows = rows.Where(r => r.EmployeeId == employeeId.Value);
		}
		else if (employeeIds is { Count: > 0 })
		{
			rows = rows.Where(r => employeeIds.Contains(r.EmployeeId));
		}

		return ApplySearch(rows, search);
	}

	private static IEnumerable<EmployeeLeavePageRow> ApplySearch(
		IEnumerable<EmployeeLeavePageRow> rows,
		string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return rows;
		}

		var s = search.Trim();
		return rows.Where(r =>
			(r.Description != null && r.Description.Contains(s)) ||
			r.EmployeeCode.Contains(s) ||
			(r.FirstName != null && r.FirstName.Contains(s)) ||
			(r.LastName != null && r.LastName.Contains(s)) ||
			(r.MiddleName != null && r.MiddleName.Contains(s)));
	}

	private static (int TotalCount, List<Guid> PageIds) PageRows(
		IEnumerable<EmployeeLeavePageRow> rows,
		int skip,
		int take)
	{
		var ordered = rows.OrderByDescending(r => r.CreationTime).ToList();
		return (ordered.Count, ordered.Skip(skip).Take(take).Select(r => r.Id).ToList());
	}
}

internal sealed record EmployeeLeavePageRow(
	Guid Id,
	Guid EmployeeId,
	DateTimeOffset StartDate,
	DateTimeOffset EndDate,
	DateTime CreationTime,
	string? Description,
	EmployeeLeaveStatusType Status,
	Guid LeavePolicyId,
	Guid? AcademicYearId,
	Guid? MasterLeaveTypeId,
	string EmployeeCode,
	string? FirstName,
	string? LastName,
	string? MiddleName);
