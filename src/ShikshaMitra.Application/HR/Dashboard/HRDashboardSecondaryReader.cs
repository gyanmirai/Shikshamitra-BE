using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardSecondaryReader(
	IEmployeeLeaveRepository leaveRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRepository,
	IAsyncQueryableExecuter executer,
	ICurrentUser currentUser) : ITransientDependency
{
	internal async Task<int> CountPendingLeavesAsync(List<Guid> scopedIds, UserScopeContext scope)
	{
		// Preserve the Leave Center's own/reporting hierarchy boundary as well as UserScope.
		if (!HrUserScopeAccess.CanReadAllLeaveRequests(scope))
		{
			var employeeQuery = await employeeRepository.GetQueryableAsync();
			var links = await executer.ToListAsync(employeeQuery.Select(x => new { x.Id, x.UserId, x.ReportingManagerId }));
			var actor = links.FirstOrDefault(x => x.UserId == currentUser.Id)
				?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);
			var allowed = new HashSet<Guid> { actor.Id };
			var children = links.Where(x => x.ReportingManagerId.HasValue).ToLookup(x => x.ReportingManagerId!.Value, x => x.Id);
			var queue = new Queue<Guid>();
			queue.Enqueue(actor.Id);
			while (queue.TryDequeue(out var manager))
				foreach (var child in children[manager])
					if (allowed.Add(child)) queue.Enqueue(child);
			scopedIds = scopedIds.Where(allowed.Contains).ToList();
		}
		var query = await leaveRepository.GetQueryableAsync();
		return await executer.CountAsync(query.Where(x => scopedIds.Contains(x.EmployeeId) && x.Status == EmployeeLeaveStatusType.Pending));
	}

	internal async Task<HRDashboardPayrollCycleDto> ReadPayrollAsync(string month, List<Guid> employeeIds)
	{
		var query = await payrollRepository.GetQueryableAsync();
		var rows = await executer.ToListAsync(query.Where(x => x.MonthYear == month && employeeIds.Contains(x.EmployeeId))
			.Select(x => new { x.IsLocked, x.ReviewStatus, x.CreationTime, x.LastModificationTime }));
		return new HRDashboardPayrollCycleDto
		{
			Month = month, SavedStaffCount = rows.Count,
			ReviewedCount = rows.Count(x => !x.IsLocked && x.ReviewStatus == "REVIEWED"),
			LockedCount = rows.Count(x => x.IsLocked),
			PendingCount = rows.Count(x => !x.IsLocked && x.ReviewStatus != "REVIEWED"),
			LastUpdatedAt = rows.Count == 0 ? null :
				new DateTimeOffset(DateTime.SpecifyKind(rows.Max(x => x.LastModificationTime ?? x.CreationTime), DateTimeKind.Utc))
		};
	}
}
