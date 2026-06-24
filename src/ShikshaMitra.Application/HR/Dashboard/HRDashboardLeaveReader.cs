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

public class HRDashboardLeaveReader(
	IAsyncQueryableExecuter asyncExecuter,
	ReadSafeLeaveUnitResolver readSafeLeaveUnitResolver) : ITransientDependency
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
			.GroupBy(l => l.PaidType)
			.Select(g => new LeaveTypeCountRow(g.Key, g.Count())));
		var recentPendingRows = await asyncExecuter.ToListAsync(pendingQuery
			.OrderByDescending(l => l.CreationTime)
			.Take(6)
			.Select(l => new LeaveDashboardRow(
				l.Id,
				l.PublicId,
				l.EmployeeId,
				l.LeavePolicyId,
				l.PaidType,
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
					l.PaidType,
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
				.GroupBy(l => l.PaidType)
				.Select(g => new LeaveTypeCountRow(g.Key, g.Count())))
			.GroupBy(l => l.PaidType)
			.Select(g => new LeaveTypeCountRow(g.Key, g.Sum(x => x.Count)))
			.ToList();
		var pendingEmployeeIds = recentPendingRows.Select(l => l.EmployeeId).Distinct().ToList();
		var pendingEmployees = await asyncExecuter.ToListAsync(
			scopedEmployeeQuery
				.Where(e => pendingEmployeeIds.Contains(e.Id))
				.Select(e => new { e.Id, e.EmployeeName, e.EmployeeCode }));
		var employeeById = pendingEmployees.ToDictionary(e => e.Id);
		var pendingPolicyIds = recentPendingRows.Select(l => l.LeavePolicyId).Distinct().ToList();
		var pendingPolicyRows = await asyncExecuter.ToListAsync(
			leavePolicyQuery
				.Where(p => pendingPolicyIds.Contains(p.Id))
				.Select(p => new { p.Id, p.AcademicYearId }));
		var academicYearIdByLeavePolicyId = pendingPolicyRows.ToDictionary(p => p.Id, p => p.AcademicYearId);

		var recentPendingRequests = new List<HRDashboardLeaveRequestDto>();
		foreach (var leave in recentPendingRows)
		{
			employeeById.TryGetValue(leave.EmployeeId, out var employee);
			var academicYearId = academicYearIdByLeavePolicyId.TryGetValue(
				leave.LeavePolicyId,
				out var resolvedAcademicYearId)
				? resolvedAcademicYearId
				: (Guid?)null;
			recentPendingRequests.Add(new HRDashboardLeaveRequestDto
			{
				Id = leave.Id,
				PublicId = leave.PublicId,
				EmployeeId = leave.EmployeeId,
				EmployeeName = employee?.EmployeeName ?? "Employee",
				EmployeeIdentifier = employee?.EmployeeCode ?? string.Empty,
				LeaveType = ToPaidType(leave.PaidType),
				StartDate = leave.StartDate.DateTime,
				EndDate = leave.EndDate.DateTime,
				TotalDays = await readSafeLeaveUnitResolver.CalculateUnitsForReadAsync(
					leave.EmployeeId,
					leave.StartDate.DateTime,
					leave.EndDate.DateTime,
					leave.HalfDay,
					academicYearId),
				Status = leave.Status.ToString(),
				SubmittedAt = leave.CreationTime
			});
		}

		return new HRDashboardLeaveDto
		{
			Pending = pendingCount,
			Approved = monthStatusCounts.GetValueOrDefault(EmployeeLeaveStatusType.Approved),
			Rejected = monthStatusCounts.GetValueOrDefault(EmployeeLeaveStatusType.Rejected),
			LeaveTypeSummary = summaryRows
				.Select(row => new HRDashboardLeaveTypeSummaryDto
				{
					LeaveType = ToPaidType(row.PaidType),
					Count = row.Count
				})
				.OrderByDescending(x => x.Count)
				.Take(6)
				.ToList(),
			RecentPendingRequests = recentPendingRequests
		};
	}

	private static string ToPaidType(LeavePaidType paidType) =>
		paidType == LeavePaidType.Paid ? "Paid" : "Unpaid";
}
