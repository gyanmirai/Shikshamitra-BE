using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class EmployeeLeavePageQuery
{
	public static async Task<(int TotalCount, List<Guid> PageIds)> GetManagementListPageAsync(
		DbContext dbContext,
		IQueryable<EmployeeLeave> dbSet,
		DateTime rangeStart,
		DateTime rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId,
		IReadOnlyCollection<Guid>? employeeIds,
		CancellationToken cancellationToken)
	{
		var rs = rangeStart.Date;
		var re = rangeEnd.Date;

		if (IsSqlite(dbContext))
		{
			return await EmployeeLeaveSqlitePageQuery.GetManagementListPageAsync(
				dbSet, rs, re, filters, search, skip, take, employeeId, employeeIds, cancellationToken);
		}

		var filterStart = new DateTimeOffset(DateTime.SpecifyKind(rs, DateTimeKind.Utc));
		var filterEndExclusive = new DateTimeOffset(DateTime.SpecifyKind(re.AddDays(1), DateTimeKind.Utc));
		var query = dbSet.Where(x => x.StartDate >= filterStart && x.StartDate < filterEndExclusive);

		query = ApplySharedFilters(dbContext, query, filters, search, employeeId, employeeIds);
		return await PageAsync(query, skip, take, cancellationToken);
	}

	public static async Task<(int TotalCount, List<Guid> PageIds)> GetCalendarPageAsync(
		DbContext dbContext,
		IQueryable<EmployeeLeave> dbSet,
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		Guid? employeeId,
		IReadOnlyCollection<Guid>? employeeIds,
		CancellationToken cancellationToken)
	{
		if (IsSqlite(dbContext))
		{
			return await EmployeeLeaveSqlitePageQuery.GetCalendarPageAsync(
				dbSet, rangeStart, rangeEnd, filters, search, skip, take, employeeId, employeeIds, cancellationToken);
		}

		var query = dbSet.Where(x => x.StartDate <= rangeEnd && x.EndDate >= rangeStart);
		query = ApplySharedFilters(dbContext, query, filters, search, employeeId, employeeIds);
		return await PageAsync(query, skip, take, cancellationToken);
	}

	private static IQueryable<EmployeeLeave> ApplySharedFilters(
		DbContext dbContext,
		IQueryable<EmployeeLeave> query,
		LeaveManagementListFilters filters,
		string? search,
		Guid? employeeId,
		IReadOnlyCollection<Guid>? employeeIds)
	{
		query = query.Where(x => x.LeavePolicy != null && x.LeavePolicy.AcademicYearId == filters.AcademicYearId);
		query = ApplyOptionalFilters(query, filters);

		if (employeeId.HasValue)
		{
			query = query.Where(x => x.EmployeeId == employeeId.Value);
		}
		else if (employeeIds is { Count: > 0 })
		{
			query = query.Where(x => employeeIds.Contains(x.EmployeeId));
		}

		return ApplySearchFilter(dbContext, query, search);
	}

	private static IQueryable<EmployeeLeave> ApplyOptionalFilters(
		IQueryable<EmployeeLeave> query,
		LeaveManagementListFilters filters)
	{
		if (filters.Status.HasValue)
		{
			query = query.Where(x => x.Status == filters.Status.Value);
		}

		if (filters.LeavePolicyId.HasValue)
		{
			query = query.Where(x => x.LeavePolicyId == filters.LeavePolicyId.Value);
		}

		if (filters.PaidType.HasValue)
		{
			query = query.Where(x => x.PaidType == filters.PaidType.Value);
		}

		return query;
	}

	private static IQueryable<EmployeeLeave> ApplySearchFilter(
		DbContext dbContext,
		IQueryable<EmployeeLeave> query,
		string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return query;
		}

		var s = search.Trim();
		var matchingEmployeeIds = dbContext.Set<Employee>()
			.Where(e =>
				e.EmployeeCode.Contains(s) ||
				(e.FirstName != null && e.FirstName.Contains(s)) ||
				(e.LastName != null && e.LastName.Contains(s)) ||
				(e.MiddleName != null && e.MiddleName.Contains(s)))
			.Select(e => e.Id);

		return query.Where(x =>
			(x.Description != null && x.Description.Contains(s)) ||
			matchingEmployeeIds.Contains(x.EmployeeId));
	}

	private static async Task<(int TotalCount, List<Guid> PageIds)> PageAsync(
		IQueryable<EmployeeLeave> query,
		int skip,
		int take,
		CancellationToken cancellationToken)
	{
		var totalCount = await query.CountAsync(cancellationToken);
		var pageIds = await query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skip)
			.Take(take)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);

		return (totalCount, pageIds);
	}

	private static bool IsSqlite(DbContext dbContext) =>
		dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
}
