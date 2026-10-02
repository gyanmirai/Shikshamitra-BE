using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
public class HRDashboardOperationalReader(
	HRDashboardScopeService scopeService,
	HRDashboardOperationalStaffReader staffReader,
	HRDashboardSecondaryReader secondaryReader) : ApplicationService
{
	public async Task<HRDashboardOverviewDto> GetOverviewAsync(HRDashboardOverviewInput input)
	{
		var date = ParseDate(input.Date);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var capabilities = await ReadCapabilitiesAsync();
		var result = new HRDashboardOverviewDto
		{
			Date = date.ToString("yyyy-MM-dd"), GeneratedAt = UtcNow(),
			ScopeLabel = HrUserScopeAccess.HasHrWideAccess(scope) ? "All authorized staff" : "Staff in your assigned scope",
			Capabilities = capabilities
		};
		if (!(capabilities.CanViewAttendance || capabilities.CanViewStaff || capabilities.CanViewEmployees ||
			capabilities.CanViewPayroll || capabilities.CanViewLeave)) return result;

		var population = await staffReader.LoadPopulationAsync(input, scope, date.AddDays(-6), date);
		result.Filters = population.Filters;
		var eligible = population.Employees.Where(x => HRDashboardOperationalStaffReader.IsEligible(x, date)).ToList();
		if (capabilities.CanViewEmployees)
		{
			result.WorkforceState = HRDashboardSectionState.Available;
			result.StaffCount = eligible.Count;
		}
		if (capabilities.CanViewAttendance)
		{
			try
			{
				var dates = Enumerable.Range(0, 7).Select(offset => date.AddDays(offset - 6)).ToList();
				var days = await staffReader.ReadDaysAsync(population, dates);
				result.Summary = HRDashboardDailyStatusProjection.Count(result.Date, days[date]);
				result.Trend = dates.Select(day => HRDashboardDailyStatusProjection.Count(day.ToString("yyyy-MM-dd"), days[day])).ToList();
				result.AttendanceState = HRDashboardSectionState.Available;
				AddAttention(result, "NeedsSetup", "Attendance calendar needs configuration", result.Summary.NeedsSetup, "NeedsSetup");
				AddAttention(result, "NeedsReview", "Attendance records need review", result.Summary.NeedsReview, "Review");
				AddAttention(result, "NotMarked", "Staff attendance is not marked", result.Summary.NotMarked, "NotMarked");
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				Logger.LogError(exception, "Could not read operational HR attendance.");
				result.AttendanceState = HRDashboardSectionState.Failed;
			}
		}
		if (capabilities.CanViewLeave)
		{
			try
			{
				result.PendingLeaveRequests = await secondaryReader.CountPendingLeavesAsync(eligible.Select(x => x.Id).ToList(), scope);
				result.LeaveState = HRDashboardSectionState.Available;
				AddAttention(result, "Leave", "Leave requests await a decision", result.PendingLeaveRequests.Value, "Leave");
			}
			catch (BusinessException exception)
			{
				Logger.LogWarning(exception, "Could not resolve dashboard leave access.");
				result.LeaveState = HRDashboardSectionState.Unavailable;
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				Logger.LogError(exception, "Could not read HR leave requests.");
				result.LeaveState = HRDashboardSectionState.Failed;
			}
		}
		if (capabilities.CanViewPayroll)
		{
			try
			{
				result.Payroll = await secondaryReader.ReadPayrollAsync(date.ToString("yyyy-MM"), eligible.Select(x => x.Id).ToList());
				result.PayrollState = HRDashboardSectionState.Available;
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				Logger.LogError(exception, "Could not read saved HR payroll cycle.");
				result.PayrollState = HRDashboardSectionState.Failed;
			}
		}
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View)]
	public async Task<HRDashboardStaffPageDto> GetStaffAsync(HRDashboardStaffInput input)
	{
		var date = ParseDate(input.Date);
		if (input.MaxResultCount is < 1 or > 100 || input.SkipCount < 0 ||
			(input.Status != null && input.Status != "" && !HRDashboardDailyStatusProjection.Statuses.Contains(input.Status) && input.Status != "Review") ||
			!new[] { "name", "name desc", "status", "status desc" }.Contains(input.Sorting) ||
			input.Search?.Length > 150)
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var population = await staffReader.LoadPopulationAsync(input, scope, date, date);
		var rows = (await staffReader.ReadDaysAsync(population, [date]))[date];
		if (input.Status == "Review")
		{
			rows = rows.Where(x => x.ReviewFlags.Count > 0).ToList();
			// Review is a secondary flag rather than a mutually exclusive daily status.
			input.Status = null;
		}
		return HRDashboardDailyStatusProjection.Page(rows, input, date.ToString("yyyy-MM-dd"), UtcNow());
	}

	private async Task<HRDashboardCapabilitiesDto> ReadCapabilitiesAsync()
	{
		return new()
		{
			CanViewAttendance = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View),
			CanViewStaff = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View),
			CanViewEmployees = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View),
			CanMarkAttendance = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.View) &&
				await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Add),
			CanViewLeave = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View),
			CanViewPayroll = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_Dashboard.View),
			CanOpenPayroll = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)
		};
	}

	private Task<bool> IsGrantedAsync(string permission) => AuthorizationService.IsGrantedAsync(permission);
	private DateTimeOffset UtcNow() => new(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc));
	private DateOnly ParseDate(string? input)
	{
		var today = DateOnly.FromDateTime(UtcNow().ToOffset(TimeSpan.FromHours(5.5)).DateTime);
		if (string.IsNullOrWhiteSpace(input)) return today;
		if (!DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date > today)
			throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
		return date;
	}

	private static void AddAttention(HRDashboardOverviewDto result, string kind, string title, int count, string action)
	{
		if (count > 0) result.Attention.Add(new() { Kind = kind, Title = title, Count = count, Action = action });
	}
}
