using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View)]
public class HRAttendanceDashboardAppService(
	HRDashboardScopeService scopeService,
	HRDashboardOperationalStaffReader staffReader,
	IRepository<AcademicYear, Guid> yearRepository) : ApplicationService, IHRAttendanceDashboardAppService
{
	private const int TrendDays = 7;
	private const int ReviewLimit = 5;
	private static readonly TimeSpan SchoolOffset = TimeSpan.FromMinutes(330);

	public virtual async Task<HRAttendanceDashboardDto> GetAsync(string? date)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View);
		var scope = await scopeService.EnsureCanReadScopedEmployeesAsync();
		var canViewReview = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View);
		var now = DateTimeOffset.UtcNow;
		var today = DateOnly.FromDateTime(now.ToOffset(SchoolOffset).DateTime);
		var selected = today;
		if (!string.IsNullOrEmpty(date) &&
			(!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out selected) ||
				selected > today || selected.DayNumber < TrendDays - 1))
			throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);

		var dates = Enumerable.Range(0, TrendDays).Select(offset => selected.AddDays(offset - TrendDays + 1)).ToList();
		var wide = HrUserScopeAccess.HasHrWideAccess(scope);
		var populations = await ReadPopulationsAsync(scope, wide, dates);
		var unavailable = dates.Where(day => !populations.ContainsKey(day)).Select(FormatDate).ToList();
		var days = await staffReader.ReadClassifiedDaysAsync(
			dates.ToDictionary(day => day, day => populations.GetValueOrDefault(day) ?? []));
		var counts = dates.Select(day =>
			HRDashboardDailyStatusProjection.Count(FormatDate(day),
				days[day].Select(row => (row.Status, row.Flags.Count > 0)))).ToList();

		return new(FormatDate(today), now, wide ? "All authorized staff" : "Staff in your assigned scope",
			counts[^1], counts, unavailable, canViewReview ? ReadReview(days[selected]) : null);
	}

	private async Task<Dictionary<DateOnly, List<Employee>>> ReadPopulationsAsync(
		UserScopeContext scope, bool wide, List<DateOnly> dates)
	{
		if (wide)
		{
			var employees = await staffReader.LoadEmployeesAsync(scope, dates[0], dates[^1]);
			return dates.ToDictionary(date => date, _ => employees);
		}

		var start = dates[0].ToDateTime(TimeOnly.MinValue);
		var end = dates[^1].ToDateTime(TimeOnly.MaxValue);
		var query = await yearRepository.GetQueryableAsync();
		var years = await AsyncExecuter.ToListAsync(query
			.Where(year => year.StartDate <= end && year.EndDate >= start)
			.Select(year => new { year.Id, year.StartDate, year.EndDate }));
		var result = new Dictionary<DateOnly, List<Employee>>();
		var yearByDate = dates.Select(date => (Date: date, YearIds: years
			.Where(year => DateOnly.FromDateTime(year.StartDate) <= date && DateOnly.FromDateTime(year.EndDate) >= date)
			.Select(year => year.Id).ToList()));
		foreach (var yearDays in yearByDate.Where(day => day.YearIds.Count == 1).GroupBy(day => day.YearIds[0]))
		{
			var employees = await staffReader.LoadEmployeesAsync(
				scope, yearDays.First().Date, yearDays.Last().Date, academicYearId: yearDays.Key);
			foreach (var day in yearDays) result[day.Date] = employees;
		}
		return result;
	}

	private static List<HRAttendanceDashboardReviewDto> ReadReview(IEnumerable<DashboardAttendanceDay> rows) =>
		rows.Where(row => row.Flags.Count > 0)
			.Select(row => new HRAttendanceDashboardReviewDto(
				row.Employee.Id, HRDashboardOperationalStaffReader.StaffName(row.Employee), row.Status, row.Flags))
			.OrderByDescending(row => row.Flags.Any(flag => flag.Code != HRDashboardReviewFlagCode.CalendarNeedsConfiguration))
			.ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
			.ThenBy(row => row.EmployeeId)
			.Take(ReviewLimit)
			.ToList();

	private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
