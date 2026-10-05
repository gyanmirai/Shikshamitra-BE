using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
public class HRDashboardOperationalReader(
	HRDashboardScopeService scopeService,
	HRDashboardOperationalStaffReader staffReader,
	HRDashboardSecondaryReader secondaryReader,
	HRDashboardModuleReader moduleReader) : ApplicationService
{
	public async Task<HRDashboardOverviewDto> GetOverviewAsync(HRDashboardOverviewInput input)
	{
		var date = ParseDate(input.Date);
		var now = DateTimeOffset.UtcNow;
		var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(5.5)).DateTime);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var capabilities = await ReadCapabilitiesAsync();
		capabilities.CanOpenOnboarding = capabilities.CanViewOnboarding && HrUserScopeAccess.HasHrWideAccess(scope);
		var result = new HRDashboardOverviewDto
		{
			Date = date.ToString("yyyy-MM-dd"), AsOfDate = today.ToString("yyyy-MM-dd"), GeneratedAt = now,
			ScopeLabel = HrUserScopeAccess.HasHrWideAccess(scope) ? "All authorized staff" : "Staff in your assigned scope",
			Capabilities = capabilities
		};
		List<Employee>? employees = null;
		if (capabilities.CanViewEmployees || capabilities.CanViewLeave || capabilities.CanViewPayroll || capabilities.CanViewAppraisals)
		{
			try { employees = await moduleReader.ReadEmployeesAsync(scope); }
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				Logger.LogError(exception, "Could not read scoped HR employees.");
				if (capabilities.CanViewEmployees) result.WorkforceState = HRDashboardSectionState.Failed;
				if (capabilities.CanViewLeave) result.LeaveState = HRDashboardSectionState.Failed;
				if (capabilities.CanViewAppraisals) result.AppraisalState = HRDashboardSectionState.Failed;
				if (capabilities.CanViewPayroll) result.PayrollState = HRDashboardSectionState.Failed;
			}
		}
		if (capabilities.CanViewEmployees && employees != null)
		{
			result.Workforce = HRDashboardModuleReader.CountWorkforce(employees, today);
			result.WorkforceState = HRDashboardSectionState.Available;
		}
		if (capabilities.CanViewAttendance)
		{
			var state = await ReadSectionAsync("attendance", async () =>
			{
				// Attendance alone uses the selected historical date. No dashboard staff filters are applied.
				var population = await staffReader.LoadPopulationAsync(new() { Date = input.Date }, scope, date.AddDays(-6), date);
				var dates = Enumerable.Range(0, 7).Select(offset => date.AddDays(offset - 6)).ToList();
				var days = await staffReader.ReadDaysAsync(population, dates);
				result.Summary = HRDashboardDailyStatusProjection.Count(result.Date, days[date]);
				result.Trend = dates.Select(day => HRDashboardDailyStatusProjection.Count(day.ToString("yyyy-MM-dd"), days[day])).ToList();
			});
			result.AttendanceState = state;
		}
		var ids = employees?.Select(x => x.Id).ToList();
		if (capabilities.CanViewLeave && ids != null)
			result.LeaveState = await ReadSectionAsync("leave", async () =>
				result.Leave = new(await secondaryReader.CountPendingLeavesAsync(ids, scope)));
		if (capabilities.CanViewPayroll && ids != null)
			result.PayrollState = await ReadSectionAsync("payroll", async () =>
				result.Payroll = await secondaryReader.ReadPayrollAsync(today.ToString("yyyy-MM"), ids));
		if (capabilities.CanViewAppraisals && ids != null)
			result.AppraisalState = await ReadSectionAsync("appraisals", async () =>
				result.Appraisal = await moduleReader.ReadAppraisalsAsync(ids, today));
		if (capabilities.CanViewOnboarding)
		{
			if (!capabilities.CanOpenOnboarding)
			{
				result.OnboardingState = HRDashboardSectionState.Unavailable;
				result.OnboardingUnavailableReason = "Onboarding summary requires school-wide HR access.";
			}
			else result.OnboardingState = await ReadSectionAsync("onboarding", async () =>
				result.Onboarding = await moduleReader.ReadOnboardingAsync());
		}
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View)]
	public async Task<HRDashboardStaffPageDto> GetStaffAsync(HRDashboardStaffInput input)
	{
		var date = ParseDate(input.Date);
		if (input.MaxResultCount is < 1 or > 100 || input.SkipCount < 0 ||
			(input.Status != null && input.Status != "" && !HRDashboardDailyStatusProjection.Statuses.Contains(input.Status) && input.Status != "Review") ||
			!new[] { "name", "name desc", "status", "status desc" }.Contains(input.Sorting) || input.Search?.Length > 150)
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var population = await staffReader.LoadPopulationAsync(input, scope, date, date);
		var rows = (await staffReader.ReadDaysAsync(population, [date]))[date];
		if (input.Status == "Review")
		{
			rows = rows.Where(x => x.ReviewFlags.Count > 0).ToList();
			input.Status = null;
		}
		return HRDashboardDailyStatusProjection.Page(rows, input, date.ToString("yyyy-MM-dd"), DateTimeOffset.UtcNow);
	}

	private async Task<HRDashboardSectionState> ReadSectionAsync(string section, Func<Task> read)
	{
		try { await read(); return HRDashboardSectionState.Available; }
		catch (BusinessException exception)
		{
			Logger.LogWarning(exception, "Could not resolve HR dashboard {Section}.", section);
			return HRDashboardSectionState.Unavailable;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			Logger.LogError(exception, "Could not read HR dashboard {Section}.", section);
			return HRDashboardSectionState.Failed;
		}
	}

	private async Task<HRDashboardCapabilitiesDto> ReadCapabilitiesAsync() => new()
	{
		CanViewAttendance = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View),
		CanViewStaff = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View),
		CanViewEmployees = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View),
		CanAddEmployees = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View) &&
			await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add),
		CanMarkAttendance = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.View) &&
			await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Add),
		CanViewLeave = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View),
		CanViewPayroll = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_Dashboard.View),
		CanOpenPayroll = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View),
		CanViewOnboarding = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View),
		CanViewAppraisals = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.View) ||
			await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View)
	};

	private Task<bool> IsGrantedAsync(string permission) => AuthorizationService.IsGrantedAsync(permission);
	private DateOnly ParseDate(string? input)
	{
		var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).DateTime);
		if (string.IsNullOrWhiteSpace(input)) return today;
		if (!DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date > today)
			throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
		return date;
	}
}
