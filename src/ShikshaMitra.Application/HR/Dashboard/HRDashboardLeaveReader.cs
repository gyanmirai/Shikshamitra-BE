using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardLeaveReader(IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HRDashboardLeaveDto> BuildLeaveAsync(
		IQueryable<EmployeeLeave> leaveQuery,
		IQueryable<LeavePolicy> leavePolicyQuery,
		IQueryable<Employee> scopedEmployeeQuery,
		IReadOnlyCollection<Guid> activeEmployeeIds,
		DateOnly monthStart,
		DateOnly monthEnd)
	{
		var pendingQuery = leaveQuery.Where(l =>
			l.Status == EmployeeLeaveStatusType.Pending &&
			activeEmployeeIds.Contains(l.EmployeeId));
		var pendingCount = await asyncExecuter.CountAsync(pendingQuery);
		var pendingSummaryRows = await asyncExecuter.ToListAsync(pendingQuery
			.GroupBy(l => l.LeavePolicyId)
			.Select(g => new LeaveTypeCountRow(g.Key, g.Count())));
		var recentPendingRows = await asyncExecuter.ToListAsync(pendingQuery
			.OrderByDescending(l => l.CreationTime)
			.Take(6)
			.Select(l => new LeaveDashboardRow(
				l.Id,
				l.PublicId,
				l.EmployeeId,
				l.LeavePolicyId,
				l.StartDate,
				l.EndDate,
				l.HalfDay,
				l.Status,
				l.CreationTime)));
		var monthRows = await asyncExecuter.ToListAsync(
			leaveQuery
				.Where(l =>
					(l.Status == EmployeeLeaveStatusType.Approved || l.Status == EmployeeLeaveStatusType.Rejected) &&
					l.StartDateOnly <= monthEnd &&
					l.EndDateOnly >= monthStart &&
					activeEmployeeIds.Contains(l.EmployeeId))
				.Select(l => new LeaveDashboardRow(
					l.Id,
					l.PublicId,
					l.EmployeeId,
					l.LeavePolicyId,
					l.StartDate,
					l.EndDate,
					l.HalfDay,
					l.Status,
					l.CreationTime)));
		var monthStatusCounts = monthRows
			.GroupBy(l => l.Status)
			.ToDictionary(g => g.Key, g => g.Count());
		var summaryRows = pendingSummaryRows
			.Concat(monthRows
				.GroupBy(l => l.LeavePolicyId)
				.Select(g => new LeaveTypeCountRow(g.Key, g.Count())))
			.GroupBy(l => l.LeavePolicyId)
			.Select(g => new LeaveTypeCountRow(g.Key, g.Sum(x => x.Count)))
			.ToList();
		var policyIds = summaryRows
			.Select(x => x.LeavePolicyId)
			.Concat(recentPendingRows.Select(x => x.LeavePolicyId))
			.Distinct()
			.ToList();
		var policyNames = await asyncExecuter.ToListAsync(
			leavePolicyQuery
				.Where(p => policyIds.Contains(p.Id))
				.Select(p => new { p.Id, p.PolicyName }));
		var policyNameById = policyNames.ToDictionary(x => x.Id, x => x.PolicyName);
		var pendingEmployeeIds = recentPendingRows.Select(l => l.EmployeeId).Distinct().ToList();
		var pendingEmployees = await asyncExecuter.ToListAsync(
			scopedEmployeeQuery
				.Where(e => pendingEmployeeIds.Contains(e.Id))
				.Select(e => new { e.Id, e.EmployeeName, e.EmployeeCode }));
		var employeeById = pendingEmployees.ToDictionary(e => e.Id);

		return new HRDashboardLeaveDto
		{
			Pending = pendingCount,
			Approved = monthStatusCounts.GetValueOrDefault(EmployeeLeaveStatusType.Approved),
			Rejected = monthStatusCounts.GetValueOrDefault(EmployeeLeaveStatusType.Rejected),
			LeaveTypeSummary = summaryRows
				.Select(row => new HRDashboardLeaveTypeSummaryDto
				{
					LeaveType = policyNameById.GetValueOrDefault(row.LeavePolicyId, "Leave"),
					Count = row.Count
				})
				.OrderByDescending(x => x.Count)
				.Take(6)
				.ToList(),
			RecentPendingRequests = recentPendingRows.Select(l =>
			{
				employeeById.TryGetValue(l.EmployeeId, out var employee);
				return new HRDashboardLeaveRequestDto
				{
					Id = l.Id,
					PublicId = l.PublicId,
					EmployeeId = l.EmployeeId,
					EmployeeName = employee?.EmployeeName ?? "Employee",
					EmployeeIdentifier = employee?.EmployeeCode ?? string.Empty,
					LeaveType = policyNameById.GetValueOrDefault(l.LeavePolicyId, "Leave"),
					StartDate = l.StartDate.DateTime,
					EndDate = l.EndDate.DateTime,
					TotalDays = LeaveDurationCalculator.CountLeaveUnits(l.StartDate.Date, l.EndDate.Date, l.HalfDay),
					Status = l.Status.ToString(),
					SubmittedAt = l.CreationTime
				};
			}).ToList()
		};
	}
}
