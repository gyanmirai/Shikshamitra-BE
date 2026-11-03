using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ShikshaMitra.Students.Attendance;

public partial class EfCoreStudentLeaveRepository
{
	public async Task<StudentLeavePortalItem?> FindPortalItemAsync(Guid tenantId, Guid studentId, Guid id,
		CancellationToken cancellationToken = default) =>
		await ProjectPortal((await GetQueryableAsync()).Where(leave => leave.TenantId == tenantId && leave.StudentId == studentId && leave.Id == id))
			.SingleOrDefaultAsync(GetCancellationToken(cancellationToken));

	public async Task<List<StudentLeavePortalCalendarItem>> GetPortalCalendarAsync(Guid tenantId, Guid studentId,
		DateTimeOffset start, DateTimeOffset endExclusive, CancellationToken cancellationToken = default)
	{
		var query = (await GetQueryableAsync()).Where(leave => leave.TenantId == tenantId && leave.StudentId == studentId &&
			leave.Status != StudentLeaveStatusEnum.Cancelled && leave.Status != StudentLeaveStatusEnum.Rejected);
		var isSqlite = (await GetDbContextAsync()).Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		if (!isSqlite) query = query.Where(leave => leave.LeaveDate < endExclusive && (leave.EndDate ?? leave.LeaveDate) >= start);
		var rows = await query.Select(leave => new StudentLeavePortalCalendarItem(leave.Id, leave.Status, leave.LeaveDate, leave.EndDate))
			.ToListAsync(GetCancellationToken(cancellationToken));
		return isSqlite ? rows.Where(leave => leave.LeaveDate < endExclusive && (leave.EndDate ?? leave.LeaveDate) >= start).ToList() : rows;
	}

	public async Task<bool> HasPortalOverlapAsync(Guid tenantId, Guid studentId, Guid? excludedId,
		DateTimeOffset start, DateTimeOffset endInclusive, CancellationToken cancellationToken = default)
	{
		var query = (await GetQueryableAsync()).Where(leave => leave.TenantId == tenantId && leave.StudentId == studentId && leave.Id != excludedId &&
			(leave.Status == StudentLeaveStatusEnum.Pending || leave.Status == StudentLeaveStatusEnum.Approved));
		var cancellation = GetCancellationToken(cancellationToken);
		var isSqlite = (await GetDbContextAsync()).Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		if (!isSqlite) return await query.AnyAsync(leave => leave.LeaveDate <= endInclusive && (leave.EndDate ?? leave.LeaveDate) >= start, cancellation);
		var dates = await query.Select(leave => new { leave.LeaveDate, leave.EndDate }).ToListAsync(cancellation);
		return dates.Any(leave => leave.LeaveDate <= endInclusive && (leave.EndDate ?? leave.LeaveDate) >= start);
	}

	public async Task<StudentLeavePortalPage> GetPortalPageAsync(Guid tenantId, Guid studentId, DateTimeOffset start,
		DateTimeOffset endExclusive, StudentLeaveStatusEnum? status, int skipCount, int maxResultCount,
		CancellationToken cancellationToken = default)
	{
		var query = (await GetQueryableAsync()).Where(leave => leave.TenantId == tenantId && leave.StudentId == studentId);
		if (status.HasValue) query = query.Where(leave => leave.Status == status.Value);
		var cancellation = GetCancellationToken(cancellationToken);
		var isSqlite = (await GetDbContextAsync()).Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
		if (!isSqlite)
		{
			query = query.Where(leave => leave.LeaveDate >= start && leave.LeaveDate < endExclusive);
			var total = await query.LongCountAsync(cancellation);
			var page = await ProjectPortal(query.OrderByDescending(leave => leave.CreationTime).Skip(skipCount).Take(maxResultCount)).ToListAsync(cancellation);
			return new(total, page);
		}
		// SQLite cannot compare DateTimeOffset. Match the existing school-date semantics
		// using narrow metadata, then read only the requested page's descriptive fields.
		var dates = await query.Select(leave => new { leave.Id, leave.LeaveDate, leave.CreationTime }).ToListAsync(cancellation);
		var matching = dates.Where(leave => leave.LeaveDate >= start && leave.LeaveDate < endExclusive).OrderByDescending(leave => leave.CreationTime).ToList();
		var ids = matching.Skip(skipCount).Take(maxResultCount).Select(leave => leave.Id).ToArray();
		if (ids.Length == 0) return new(matching.Count, []);
		var rows = await ProjectPortal(query.Where(leave => Enumerable.Contains(ids, leave.Id))).ToListAsync(cancellation);
		var byId = rows.ToDictionary(leave => leave.Id);
		return new(matching.Count, ids.Select(id => byId[id]).ToList());
	}

	private static IQueryable<StudentLeavePortalItem> ProjectPortal(IQueryable<StudentLeave> query) => query.Select(leave => new StudentLeavePortalItem
	{
		Id = leave.Id, StudentLeaveTypeId = leave.StudentLeaveTypeId, IsHalfDay = leave.IsHalfDay,
		LeaveDate = leave.LeaveDate, EndDate = leave.EndDate, TotalDays = leave.TotalDays,
		Description = leave.Description, Status = leave.Status, CreationTime = leave.CreationTime,
		LastModificationTime = leave.LastModificationTime, RejectionReason = leave.RejectionReason, Remarks = leave.Remarks,
		MobileAttachmentsJson = leave.MobileAttachmentsJson, HalfDayStartTime = leave.HalfDayStartTime, HalfDayEndTime = leave.HalfDayEndTime
	});
}
