using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
public class HRDashboardSummaryReader(
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository,
	IRepository<LeavePolicy, Guid> leavePolicyRepository,
	HRDashboardEmployeeScopeReader employeeScopeReader,
	HRDashboardWorkforceReader workforceReader,
	HRDashboardLeaveReader leaveReader,
	HRDashboardPayrollReader payrollReader,
	HRDashboardReportingManagerIssueReader reportingManagerIssueReader)
	: ApplicationService, ITransientDependency
{
	public async Task<HRDashboardSummaryDto> GetSummaryAsync(
		HRDashboardSummaryRequestDto? input,
		UserScopeContext scope)
	{
		input ??= new HRDashboardSummaryRequestDto();
		var dashboardDate = HRDashboardRequestNormalizer.ParseDateOrDefault(input.Date, Clock.Now.Date);
		var rangeDays = Math.Clamp(input.Range ?? HRDashboardRequestNormalizer.DefaultRangeDays, 1, HRDashboardRequestNormalizer.MaxRangeDays);
		var month = HRDashboardRequestNormalizer.NormalizeMonth(input.Month, dashboardDate);
		var selectedMonthStart = DateTime.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture);
		var selectedNextMonthStart = selectedMonthStart.AddMonths(1);
		var selectedMonthStartDate = DateOnly.FromDateTime(selectedMonthStart);
		var selectedMonthEndDate = DateOnly.FromDateTime(selectedNextMonthStart.AddDays(-1));

		var employeeQuery = await employeeScopeReader.GetScopedEmployeeQueryAsync(scope);
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var leaveQuery = await leaveRepository.GetQueryableAsync();
		var leavePolicyQuery = await leavePolicyRepository.GetQueryableAsync();

		var activeEmployees = employeeQuery.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active);
		var activeEmployeeRows = await AsyncExecuter.ToListAsync(activeEmployees
			.Select(e => new ActiveEmployeeDashboardRow(
				e.Id,
				e.EmployeeName,
				e.EmployeeCode,
				e.EmployeeGroup,
				e.DateOfJoining,
				e.DateOfBirth)));
		var activeEmployeeIds = activeEmployeeRows.Select(e => e.Id).ToList();
		var scopedEmployeeCountRows = await AsyncExecuter.ToListAsync(employeeQuery
			.GroupBy(e => e.EmployeeStatusType == EmployeeStatusType.Active ? "active" : "inactive")
			.Select(g => new { Status = g.Key, Count = g.Count() }));
		var scopedEmployeeCounts = scopedEmployeeCountRows.ToDictionary(x => x.Status, x => x.Count);

		var activeEmployeeStats = activeEmployeeRows
			.GroupBy(_ => 1)
			.Select(g => new
			{
				TotalActive = g.Count(),
				Teaching = g.Count(e => e.EmployeeGroup == EmployeeGroupType.Teaching),
				NonTeaching = g.Count(e => e.EmployeeGroup == EmployeeGroupType.NonTeaching),
				NewJoiners = g.Count(e =>
					e.DateOfJoining.HasValue &&
					e.DateOfJoining.Value >= DateOnly.FromDateTime(selectedMonthStart) &&
					e.DateOfJoining.Value < DateOnly.FromDateTime(selectedNextMonthStart))
			})
			.FirstOrDefault();
		var celebrationCandidates = activeEmployeeRows
			.Where(e => e.DateOfBirth.HasValue || e.DateOfJoining.HasValue)
			.ToList();

		var todayDateOnly = DateOnly.FromDateTime(dashboardDate);
		var trendStart = todayDateOnly.AddDays(-(rangeDays - 1));
		var trendEnd = todayDateOnly;

		var workforce = await workforceReader.BuildWorkforceAsync(
			activeEmployees,
			activeEmployeeStats?.TotalActive ?? 0,
			activeEmployeeStats?.Teaching ?? 0,
			activeEmployeeStats?.NonTeaching ?? 0,
			activeEmployeeStats?.NewJoiners ?? 0,
			scopedEmployeeCounts.GetValueOrDefault("inactive"),
			selectedMonthStart,
			selectedNextMonthStart);
		var attendanceRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a =>
				a.AttendanceDate >= trendStart &&
				a.AttendanceDate <= trendEnd &&
				activeEmployeeIds.Contains(a.EmployeeId))
				.Select(a => new AttendanceTrendRow(
					a.EmployeeId,
					a.AttendanceDate,
					a.StatusType)));
		var leaveTrendRows = await AsyncExecuter.ToListAsync(
			leaveQuery.Where(l =>
				l.Status == EmployeeLeaveStatusType.Approved &&
				l.StartDateOnly <= trendEnd &&
				l.EndDateOnly >= trendStart &&
				activeEmployeeIds.Contains(l.EmployeeId))
				.Select(l => new LeaveTrendRow(
					l.EmployeeId,
					l.StartDateOnly,
					l.EndDateOnly,
					l.HalfDay)));

		var attendanceToday = HRDashboardProjection.BuildAttendanceToday(
			attendanceRows,
			leaveTrendRows,
			todayDateOnly,
			activeEmployeeIds.Count);
		var attendanceTrend = HRDashboardProjection.BuildAttendanceTrend(
			attendanceRows,
			leaveTrendRows,
			trendStart,
			trendEnd);
		var leave = await leaveReader.BuildLeaveAsync(
			leaveQuery,
			leavePolicyQuery,
			employeeQuery,
			activeEmployeeIds,
			selectedMonthStartDate,
			selectedMonthEndDate);
		var inactiveReportingManagerIssue = await reportingManagerIssueReader.BuildInactiveReportingManagerIssueAsync(employeeQuery);
		var payroll = await payrollReader.BuildPayrollAsync(month);
		var celebrations = HRDashboardProjection.BuildCelebrations(celebrationCandidates, dashboardDate);
		var insights = HRDashboardProjection.BuildInsights(attendanceToday, leave, payroll, celebrations, inactiveReportingManagerIssue);

		return new HRDashboardSummaryDto
		{
			Date = dashboardDate.Date,
			Month = month,
			Range = rangeDays,
			Workforce = workforce,
			AttendanceToday = attendanceToday,
			AttendanceTrend = attendanceTrend,
			Leave = leave,
			Payroll = payroll,
			Celebrations = celebrations,
			Insights = insights
		};
	}
}
