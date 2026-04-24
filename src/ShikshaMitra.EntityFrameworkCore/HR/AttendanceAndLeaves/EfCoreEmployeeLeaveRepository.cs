using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EfCoreEmployeeLeaveRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EmployeeLeave, Guid>(dbContextProvider), IEmployeeLeaveRepository
{
	public async Task<bool> HasOverlappingLeaveAsync(
		Guid employeeId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		Guid? excludeLeaveId = null)
	{
		var dbSet = await GetDbSetAsync();

		var query = dbSet.Where(x =>
			x.EmployeeId == employeeId &&
			!x.IsDeleted &&
			(x.Status == EmployeeLeaveStatusType.Pending ||
			 x.Status == EmployeeLeaveStatusType.Approved ||
			 x.Status == EmployeeLeaveStatusType.Reviewed) &&
			((x.StartDate <= startDate && x.EndDate >= startDate) ||
			 (x.StartDate <= endDate && x.EndDate >= endDate) ||
			 (x.StartDate >= startDate && x.EndDate <= endDate)));

		if (excludeLeaveId.HasValue)
		{
			query = query.Where(x => x.Id != excludeLeaveId.Value);
		}

		return await query.AnyAsync();
	}

	public async Task<int> GetApprovedLeaveDaysAsync(
		Guid employeeId,
		Guid leavePolicyId,
		int year)
	{
		var dbSet = await GetDbSetAsync();

		var startOfYear = new DateTimeOffset(new DateTime(year, 1, 1));
		var endOfYear = new DateTimeOffset(new DateTime(year, 12, 31, 23, 59, 59));

		var leaves = await dbSet
			.Where(x =>
				x.EmployeeId == employeeId &&
				x.LeaveTypeId == leavePolicyId &&
				x.Status == EmployeeLeaveStatusType.Approved &&
				!x.IsDeleted &&
				x.StartDate >= startOfYear &&
				x.EndDate <= endOfYear)
			.ToListAsync();

		var totalDays = 0;
		foreach (var leave in leaves)
		{
			totalDays += LeaveDurationCalculator.CountLeaveDays(
				leave.StartDate.DateTime,
				leave.EndDate.DateTime,
				leave.HalfDay);
		}

		return totalDays;
	}

	public async Task<int> GetApprovedLeaveDaysAsync(
		Guid employeeId,
		Guid leavePolicyId)
	{
		var dbSet = await GetDbSetAsync();

		var leaves = await dbSet
			.Where(x =>
				x.EmployeeId == employeeId &&
				x.LeaveTypeId == leavePolicyId &&
				x.Status == EmployeeLeaveStatusType.Approved &&
				!x.IsDeleted)
			.ToListAsync();

		var totalDays = 0;
		foreach (var leave in leaves)
		{
			totalDays += LeaveDurationCalculator.CountLeaveDays(
				leave.StartDate.DateTime,
				leave.EndDate.DateTime,
				leave.HalfDay);
		}

		return totalDays;
	}

	public async Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForManagementListAsync(
		DateTime rangeStart,
		DateTime rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		var dbSet = await GetDbSetAsync();
		var rs = rangeStart.Date;
		var re = rangeEnd.Date;

		if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
		{
			return await FilterManagementListSqliteAsync(
				dbSet, rs, re, filters, search, skip, take, cancellationToken);
		}

		var filterStart = new DateTimeOffset(DateTime.SpecifyKind(rs, DateTimeKind.Utc));
		var filterEndExclusive = new DateTimeOffset(DateTime.SpecifyKind(re.AddDays(1), DateTimeKind.Utc));
		var query = dbSet.Where(x =>
			x.StartDate >= filterStart &&
			x.StartDate < filterEndExclusive);
		query = ApplyPolicyYearFilter(query, filters);
		query = ApplyOptionalFilters(query, filters);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var s = search.Trim();
			var employees = dbContext.Set<Employee>();
			var matchingEmployeeIds = employees.Where(e =>
					e.EmployeeCode.Contains(s) ||
					(e.FirstName != null && e.FirstName.Contains(s)) ||
					(e.LastName != null && e.LastName.Contains(s)) ||
					(e.MiddleName != null && e.MiddleName.Contains(s)))
				.Select(e => e.Id);

			query = query.Where(x =>
				(x.Description != null && x.Description.Contains(s)) ||
				matchingEmployeeIds.Contains(x.EmployeeId));
		}

		var totalCount = await query.CountAsync(cancellationToken);
		var pageIds = await query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skip)
			.Take(take)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);

		return (totalCount, pageIds);
	}

	public async Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForCalendarAsync(
		DateTimeOffset rangeStart,
		DateTimeOffset rangeEnd,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		var dbSet = await GetDbSetAsync();
		var rs = rangeStart;
		var re = rangeEnd;

		if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
		{
			return await FilterCalendarSqliteAsync(dbSet, rs, re, filters, search, skip, take, cancellationToken);
		}

		var query = dbSet.Where(x =>
			x.StartDate <= re &&
			x.EndDate >= rs);
		query = ApplyPolicyYearFilter(query, filters);
		query = ApplyOptionalFilters(query, filters);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var s = search.Trim();
			var employees = dbContext.Set<Employee>();
			var matchingEmployeeIds = employees.Where(e =>
					e.EmployeeCode.Contains(s) ||
					(e.FirstName != null && e.FirstName.Contains(s)) ||
					(e.LastName != null && e.LastName.Contains(s)) ||
					(e.MiddleName != null && e.MiddleName.Contains(s)))
				.Select(e => e.Id);

			query = query.Where(x =>
				(x.Description != null && x.Description.Contains(s)) ||
				matchingEmployeeIds.Contains(x.EmployeeId));
		}

		var totalCount = await query.CountAsync(cancellationToken);
		var pageIds = await query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skip)
			.Take(take)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);

		return (totalCount, pageIds);
	}

	private static IQueryable<EmployeeLeave> ApplyPolicyYearFilter(
		IQueryable<EmployeeLeave> query,
		LeaveManagementListFilters filters)
	{
		return query.Where(x => x.LeavePolicy != null && x.LeavePolicy.AcademicYearId == filters.AcademicYearId);
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
			query = query.Where(x => x.LeaveTypeId == filters.LeavePolicyId.Value);
		}

		if (filters.LeaveTypeMasterId.HasValue)
		{
			query = query.Where(x =>
				x.LeavePolicy != null && x.LeavePolicy.LeaveTypeId == filters.LeaveTypeMasterId.Value);
		}

		return query;
	}

	private static async Task<(int TotalCount, List<Guid> PageIds)> FilterManagementListSqliteAsync(
		IQueryable<EmployeeLeave> dbSet,
		DateTime rs,
		DateTime re,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		CancellationToken cancellationToken)
	{
		var rows = await dbSet
			.Select(x => new
			{
				x.Id,
				x.StartDate,
				x.CreationTime,
				x.Description,
				x.Status,
				x.LeaveTypeId,
				AcademicYearId = x.LeavePolicy != null ? x.LeavePolicy.AcademicYearId : (Guid?)null,
				MasterLeaveTypeId = x.LeavePolicy != null ? x.LeavePolicy.LeaveTypeId : (Guid?)null,
				EmployeeCode = x.Employee != null ? x.Employee.EmployeeCode : string.Empty,
				FirstName = x.Employee != null ? x.Employee.FirstName : null,
				LastName = x.Employee != null ? x.Employee.LastName : null,
				MiddleName = x.Employee != null ? x.Employee.MiddleName : null
			})
			.ToListAsync(cancellationToken);

		var filtered = rows.Where(r =>
		{
			var d = r.StartDate.DateTime.Date;
			return r.AcademicYearId == filters.AcademicYearId && d >= rs && d <= re;
		}).ToList();

		if (filters.Status.HasValue)
		{
			filtered = filtered.Where(r => r.Status == filters.Status.Value).ToList();
		}

		if (filters.LeavePolicyId.HasValue)
		{
			filtered = filtered.Where(r => r.LeaveTypeId == filters.LeavePolicyId.Value).ToList();
		}

		if (filters.LeaveTypeMasterId.HasValue)
		{
			filtered = filtered
				.Where(r => r.MasterLeaveTypeId == filters.LeaveTypeMasterId.Value)
				.ToList();
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var s = search.Trim();
			filtered = filtered.Where(r =>
				(r.Description != null && r.Description.Contains(s)) ||
				r.EmployeeCode.Contains(s) ||
				(r.FirstName != null && r.FirstName.Contains(s)) ||
				(r.LastName != null && r.LastName.Contains(s)) ||
				(r.MiddleName != null && r.MiddleName.Contains(s))).ToList();
		}

		var ordered = filtered.OrderByDescending(r => r.CreationTime).ToList();
		return (ordered.Count, ordered.Skip(skip).Take(take).Select(r => r.Id).ToList());
	}

	private static async Task<(int TotalCount, List<Guid> PageIds)> FilterCalendarSqliteAsync(
		IQueryable<EmployeeLeave> dbSet,
		DateTimeOffset rs,
		DateTimeOffset re,
		LeaveManagementListFilters filters,
		string? search,
		int skip,
		int take,
		CancellationToken cancellationToken)
	{
		var rows = await dbSet
			.Select(x => new
			{
				x.Id,
				x.StartDate,
				x.EndDate,
				x.CreationTime,
				x.Description,
				x.Status,
				x.LeaveTypeId,
				AcademicYearId = x.LeavePolicy != null ? x.LeavePolicy.AcademicYearId : (Guid?)null,
				MasterLeaveTypeId = x.LeavePolicy != null ? x.LeavePolicy.LeaveTypeId : (Guid?)null,
				EmployeeCode = x.Employee != null ? x.Employee.EmployeeCode : string.Empty,
				FirstName = x.Employee != null ? x.Employee.FirstName : null,
				LastName = x.Employee != null ? x.Employee.LastName : null,
				MiddleName = x.Employee != null ? x.Employee.MiddleName : null
			})
			.ToListAsync(cancellationToken);

		var filtered = rows.Where(r =>
			r.AcademicYearId == filters.AcademicYearId &&
			r.StartDate <= re &&
			r.EndDate >= rs).ToList();

		if (filters.Status.HasValue)
		{
			filtered = filtered.Where(r => r.Status == filters.Status.Value).ToList();
		}

		if (filters.LeavePolicyId.HasValue)
		{
			filtered = filtered.Where(r => r.LeaveTypeId == filters.LeavePolicyId.Value).ToList();
		}

		if (filters.LeaveTypeMasterId.HasValue)
		{
			filtered = filtered
				.Where(r => r.MasterLeaveTypeId == filters.LeaveTypeMasterId.Value)
				.ToList();
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var s = search.Trim();
			filtered = filtered.Where(r =>
				(r.Description != null && r.Description.Contains(s)) ||
				r.EmployeeCode.Contains(s) ||
				(r.FirstName != null && r.FirstName.Contains(s)) ||
				(r.LastName != null && r.LastName.Contains(s)) ||
				(r.MiddleName != null && r.MiddleName.Contains(s))).ToList();
		}

		var ordered = filtered.OrderByDescending(r => r.CreationTime).ToList();
		return (ordered.Count, ordered.Skip(skip).Take(take).Select(r => r.Id).ToList());
	}
}
