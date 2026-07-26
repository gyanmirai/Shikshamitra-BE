using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees.Dashboard;

[Authorize]
[RemoteService(false)]
public class EmployeeDashboardAppService(
	IEmployeeAuthorizationService employeeAuthorizationService,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository,
	EmployeeDashboardHolidayReader holidayReader,
	LeaveBalanceReader leaveBalanceReader)
	: ApplicationService, IEmployeeDashboardAppService
{
	public async Task<EmployeeDashboardDto> GetAsync(Guid employeeId, DateTime? date = null)
	{
		await employeeAuthorizationService.CheckEmployeeAccessAsync(employeeId);

		var dashboardDate = (date ?? Clock.Now).Date;
		var result = new EmployeeDashboardDto
		{
			EmployeeId = employeeId,
			Date = dashboardDate
		};

		var academicYear = await ResolveAcademicYearAsync(dashboardDate);
		if (academicYear is null)
		{
			result.TodayStatus = EmployeeDashboardStatus.NotAvailable;
			result.DataNotice = "No academic year contains the selected date.";
			return result;
		}

		result.AcademicYearId = academicYear.Id;
		result.AcademicYearName = academicYear.Name;

		var rangeStart = DateOnly.FromDateTime(academicYear.StartDate);
		var rangeEnd = DateOnly.FromDateTime(
			dashboardDate < academicYear.EndDate.Date ? dashboardDate : academicYear.EndDate.Date);
		var today = DateOnly.FromDateTime(dashboardDate);

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var attendanceRows = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(row =>
					row.EmployeeId == employeeId &&
					row.AttendanceDate >= rangeStart &&
					row.AttendanceDate <= rangeEnd)
				.OrderBy(row => row.AttendanceDate));

		var leaveQuery = await leaveRepository.GetQueryableAsync();
		var approvedLeaveRows = await AsyncExecuter.ToListAsync(
			leaveQuery
				.Where(leave =>
					leave.EmployeeId == employeeId &&
					leave.Status == EmployeeLeaveStatusType.Approved &&
					leave.StartDateOnly <= rangeEnd &&
					leave.EndDateOnly >= rangeStart)
				.OrderBy(leave => leave.StartDateOnly)
				.ThenBy(leave => leave.EndDateOnly));
		var attendanceFacts = EmployeeDashboardProjection.MergeAttendanceWithApprovedLeave(
			rangeStart,
			rangeEnd,
			attendanceRows
				.Select(row => new EmployeeDashboardAttendanceFact(
					row.AttendanceDate,
					row.StatusType))
				.ToList(),
			approvedLeaveRows
				.Select(leave => new EmployeeDashboardApprovedLeaveFact(
					leave.StartDateOnly,
					leave.EndDateOnly,
					leave.HalfDay))
				.ToList());
		var attendanceByDate = attendanceRows
			.GroupBy(row => row.AttendanceDate)
			.ToDictionary(group => group.Key, group => group.First());

		var holidayContext = await holidayReader.ResolveAsync(employeeId, academicYear, dashboardDate);
		var workCalendar = holidayContext.WorkCalendar;
		var holidays = holidayContext.Holidays;
		result.Holidays = holidays;
		result.UpcomingHoliday = holidays
			.Where(holiday => holiday.ToDate >= today)
			.OrderBy(holiday => holiday.FromDate)
			.ThenBy(holiday => holiday.Name)
			.FirstOrDefault();

		var todayAttendance = attendanceFacts.FirstOrDefault(row => row.Date == today);
		var isHoliday = holidays.Any(holiday =>
			holiday.FromDate <= today &&
			holiday.ToDate >= today);
		var isWeeklyOff = workCalendar is not null &&
			EmployeeDashboardProjection.IsWeeklyOff(
				dashboardDate.DayOfWeek,
				workCalendar.WeeklyOffPattern,
				workCalendar.GetCustomWeeklyOffs());

		result.TodayStatus = EmployeeDashboardProjection.ResolveTodayStatus(
			todayAttendance,
			isHoliday,
			isWeeklyOff);
		result.RecordedAttendanceDays = attendanceFacts.Count(fact =>
			fact.Source == EmployeeDashboardAttendanceFactSource.Attendance);
		result.AttendancePercentage = EmployeeDashboardProjection.CalculateAttendancePercentage(
			attendanceFacts);
		result.AttendanceDays = attendanceFacts
			.Select(fact =>
			{
				attendanceByDate.TryGetValue(fact.Date, out var attendance);
				return new EmployeeDashboardAttendanceDayDto
				{
					Date = fact.Date,
					Status = EmployeeDashboardProjection.ToStatus(fact),
					TimeIn = attendance?.TimeIn,
					TimeOut = attendance?.TimeOut,
					Remarks = attendance?.Remarks
				};
			})
			.ToList();
		result.AttendanceTrend = EmployeeDashboardProjection.BuildTrend(
			rangeStart,
			rangeEnd,
			attendanceFacts);

		result.PendingLeaveRequests = await AsyncExecuter.CountAsync(
			leaveQuery.Where(leave =>
				leave.EmployeeId == employeeId &&
				leave.StartDateOnly <= DateOnly.FromDateTime(academicYear.EndDate) &&
				leave.EndDateOnly >= rangeStart &&
				(leave.Status == EmployeeLeaveStatusType.Pending ||
				 leave.Status == EmployeeLeaveStatusType.Reviewed)));

		await PopulateLeaveBalanceAsync(result, academicYear.Id, employeeId);
		return result;
	}

	private async Task<AcademicYear?> ResolveAcademicYearAsync(DateTime dashboardDate)
	{
		var query = await academicYearRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			query
				.Where(year =>
					year.StartDate <= dashboardDate &&
					year.EndDate >= dashboardDate)
				.OrderByDescending(year => year.StartDate));
	}

	private async Task PopulateLeaveBalanceAsync(
		EmployeeDashboardDto result,
		Guid academicYearId,
		Guid employeeId)
	{
		try
		{
			var balances = await leaveBalanceReader.GetBalancesForAuthorizedEmployeeAsync(
				academicYearId,
				employeeId);
			var finiteBalances = balances.Items
				.Where(item => item.RemainingDays.HasValue)
				.Select(item => item.RemainingDays!.Value)
				.ToList();
			result.LeaveBalanceDays = finiteBalances.Count == 0 ? null : finiteBalances.Sum();
			result.HasUnlimitedLeave = balances.Items.Any(item => item.IsUnlimited);
			result.IsLeaveBalanceAvailable = true;
		}
		catch (BusinessException exception) when (IsLeaveBalanceConfigurationError(exception.Code))
		{
			result.LeaveBalanceDays = null;
			result.HasUnlimitedLeave = false;
			result.IsLeaveBalanceAvailable = false;
		}
	}

	private static bool IsLeaveBalanceConfigurationError(string? code) =>
		code is HRErrorCodes.DefaultPayrollPolicyRequired
			or HRErrorCodes.DefaultAttendancePolicyRequired
			or HRErrorCodes.DefaultLeavePolicyRequired
			or HRErrorCodes.LeavePolicyNotFound
			or HRErrorCodes.LeavePolicyInactive
			or HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch
			or HRErrorCodes.HrPolicyInvalidWorkCalendar
			or HRErrorCodes.WorkCalendarPolicyMustBeActive;

}
