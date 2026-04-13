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
		Guid leaveTypeId,
		int year)
	{
		var dbSet = await GetDbSetAsync();

		var startOfYear = new DateTimeOffset(new DateTime(year, 1, 1));
		var endOfYear = new DateTimeOffset(new DateTime(year, 12, 31));

		var leaves = await dbSet
			.Where(x =>
				x.EmployeeId == employeeId &&
				x.LeaveTypeId == leaveTypeId &&
				x.Status == EmployeeLeaveStatusType.Approved &&
				!x.IsDeleted &&
				x.StartDate >= startOfYear &&
				x.EndDate <= endOfYear)
			.ToListAsync();

		var totalDays = 0;
		foreach (var leave in leaves)
		{
			var days = (leave.EndDate.Date - leave.StartDate.Date).Days + 1;
			totalDays += leave.HalfDay ? (int)Math.Ceiling(days / 2.0) : days;
		}

		return totalDays;
	}

	public async Task<(int TotalCount, List<Guid> PageIds)> GetPagedIdsForManagementListAsync(
		DateTime rangeStart,
		DateTime rangeEnd,
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
			var rows = await dbSet
				.Select(x => new
				{
					x.Id,
					x.StartDate,
					x.CreationTime,
					x.Description,
					EmployeeCode = x.Employee != null ? x.Employee.EmployeeCode : string.Empty,
					FirstName = x.Employee != null ? x.Employee.FirstName : null,
					LastName = x.Employee != null ? x.Employee.LastName : null,
					MiddleName = x.Employee != null ? x.Employee.MiddleName : null
				})
				.ToListAsync(cancellationToken);

			var filtered = rows.Where(r =>
			{
				var d = r.StartDate.DateTime.Date;
				return d >= rs && d <= re;
			}).ToList();

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

		var filterStart = new DateTimeOffset(DateTime.SpecifyKind(rs, DateTimeKind.Utc));
		var filterEndExclusive = new DateTimeOffset(DateTime.SpecifyKind(re.AddDays(1), DateTimeKind.Utc));
		var query = dbSet.Where(x =>
			x.StartDate >= filterStart &&
			x.StartDate < filterEndExclusive);

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
}