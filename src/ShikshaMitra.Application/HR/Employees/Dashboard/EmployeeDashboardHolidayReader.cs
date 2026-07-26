using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Employees.Dashboard;

public class EmployeeDashboardHolidayReader(
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
	IRepository<Holiday, Guid> holidayRepository,
	HrAttendanceWorkCalendarResolver attendanceWorkCalendarResolver,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	internal async Task<EmployeeDashboardHolidayContext> ResolveAsync(
		Guid employeeId,
		AcademicYear academicYear,
		DateTime dashboardDate)
	{
		var workCalendar = await TryResolveWorkCalendarAsync(
			employeeId,
			academicYear.Id,
			dashboardDate);
		var holidays = workCalendar is null
			? await ResolveCentralHolidaysAsync(academicYear.Id)
			: await ResolvePolicyHolidaysAsync(workCalendar.Id);
		return new EmployeeDashboardHolidayContext(workCalendar, holidays);
	}

	private async Task<WorkCalendarPolicy?> TryResolveWorkCalendarAsync(
		Guid employeeId,
		Guid academicYearId,
		DateTime dashboardDate)
	{
		try
		{
			return await attendanceWorkCalendarResolver.ResolveAsync(
				employeeId,
				dashboardDate,
				academicYearId);
		}
		catch (BusinessException exception) when (IsConfigurationError(exception.Code))
		{
			return null;
		}
	}

	private async Task<List<EmployeeDashboardHolidayDto>> ResolvePolicyHolidaysAsync(
		Guid workCalendarId)
	{
		var query = await workCalendarHolidayRepository.GetQueryableAsync();
		var holidays = await asyncExecuter.ToListAsync(
			query
				.Where(holiday =>
					holiday.WorkCalendarPolicyId == workCalendarId &&
					holiday.IsIncluded)
				.OrderBy(holiday => holiday.FromDate)
				.ThenBy(holiday => holiday.HolidayName));
		return holidays
			.Select(holiday => new EmployeeDashboardHolidayDto
			{
				Name = holiday.HolidayName,
				FromDate = holiday.FromDate,
				ToDate = holiday.ToDate
			})
			.ToList();
	}

	private async Task<List<EmployeeDashboardHolidayDto>> ResolveCentralHolidaysAsync(
		Guid academicYearId)
	{
		var query = await holidayRepository.GetQueryableAsync();
		var holidays = await asyncExecuter.ToListAsync(
			query
				.Where(holiday => holiday.AcademicYearId == academicYearId)
				.OrderBy(holiday => holiday.StartDate)
				.ThenBy(holiday => holiday.HolidayName));
		return holidays
			.Select(holiday => new EmployeeDashboardHolidayDto
			{
				Name = holiday.HolidayName,
				FromDate = holiday.StartDate,
				ToDate = holiday.EndDate
			})
			.ToList();
	}

	private static bool IsConfigurationError(string? code) =>
		code is HRErrorCodes.DefaultAttendancePolicyRequired
			or HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch
			or HRErrorCodes.HrPolicyInvalidWorkCalendar
			or HRErrorCodes.WorkCalendarPolicyMustBeActive;
}

internal sealed record EmployeeDashboardHolidayContext(
	WorkCalendarPolicy? WorkCalendar,
	List<EmployeeDashboardHolidayDto> Holidays);
