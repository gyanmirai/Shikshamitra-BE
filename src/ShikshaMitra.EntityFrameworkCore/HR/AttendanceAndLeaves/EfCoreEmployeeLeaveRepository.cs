using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Linq;
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
}