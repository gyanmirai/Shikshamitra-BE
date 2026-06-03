using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task SeedPastAcademicYearPayrollAttendanceAsync(List<Guid> employeeIds, Guid educationMediumStageId)
	{
		employeeIds = employeeIds
			.Distinct()
			.ToList();

		if (employeeIds.Count == 0)
		{
			return;
		}

		var pastStartYear = _clock.Now.Year - 1;
		var academicYear = await _academicYearRepository.FirstOrDefaultAsync(x =>
			x.StartDate.Year == pastStartYear);
		if (academicYear == null)
		{
			return;
		}

		var monthsToSeed = PastAttendanceMonthsToSeed();
		var today = _clock.Now.Date;
		var latestCandidateEnd = academicYear.EndDate.Date < today
			? academicYear.EndDate.Date
			: new DateTime(today.Year, today.Month, 1).AddDays(-1);
		if (latestCandidateEnd < academicYear.StartDate.Date)
		{
			return;
		}

		var lastMonthStart = new DateTime(latestCandidateEnd.Year, latestCandidateEnd.Month, 1);
		var windowStartMonth = lastMonthStart.AddMonths(-(monthsToSeed - 1));
		if (windowStartMonth < new DateTime(academicYear.StartDate.Year, academicYear.StartDate.Month, 1))
		{
			windowStartMonth = new DateTime(academicYear.StartDate.Year, academicYear.StartDate.Month, 1);
		}

		var windowStart = DateOnly.FromDateTime(windowStartMonth);
		var windowEnd = DateOnly.FromDateTime(new DateTime(lastMonthStart.Year, lastMonthStart.Month, DateTime.DaysInMonth(lastMonthStart.Year, lastMonthStart.Month)));
		if (windowEnd > DateOnly.FromDateTime(academicYear.EndDate.Date))
		{
			windowEnd = DateOnly.FromDateTime(academicYear.EndDate.Date);
		}
		var holidayDates = await BuildSeedHolidayDateSetAsync(windowStart, windowEnd, academicYear.Id);

		List<EmployeeAttendance> existingRows;
		using (_dataFilter.Disable<ISoftDelete>())
		{
			existingRows = await _employeeAttendanceRepository.GetListAsync(a =>
				employeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate >= windowStart &&
				a.AttendanceDate <= windowEnd);
		}
		var existingKeys = existingRows
			.Select(a => (a.EmployeeId, a.AttendanceDate))
			.ToHashSet();

		var indiaOffset = TimeSpan.FromHours(5.5);
		var attendancesToInsert = new List<EmployeeAttendance>();
		var workingDayIndex = 0;
		for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
		{
			if (!ShouldSeedAttendanceForDate(date, holidayDates))
			{
				continue;
			}

			workingDayIndex++;
			foreach (var employeeId in employeeIds)
			{
				if (existingKeys.Contains((employeeId, date)))
				{
					continue;
				}

				var seed = StableSeedHash($"{CurrentTenant.Id:N}:{employeeId:N}:{date:yyyyMMdd}");
				var status = seed % 13 == 0
					? EmployeeAttendanceStatusType.FullDayLeave
					: seed % 7 == 0
						? EmployeeAttendanceStatusType.HalfDayLeave
						: EmployeeAttendanceStatusType.Present;

				DateTimeOffset? timeIn = null;
				DateTimeOffset? timeOut = null;
				if (status == EmployeeAttendanceStatusType.Present || status == EmployeeAttendanceStatusType.HalfDayLeave)
				{
					var lateMinutes = seed % 11 == 0 ? 35 : seed % 5 == 0 ? 12 : 0;
					var outHour = status == EmployeeAttendanceStatusType.HalfDayLeave ? 13 : 17;
					var outMinute = status == EmployeeAttendanceStatusType.HalfDayLeave ? 30 : seed % 17 == 0 ? 55 : 30;
					timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 9, lateMinutes, 0, indiaOffset);
					timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, outHour, outMinute, 0, indiaOffset);
				}

				if (seed % 19 == 0 && status == EmployeeAttendanceStatusType.Present)
				{
					timeOut = null;
				}

				attendancesToInsert.Add(new EmployeeAttendance
				{
					TenantId = CurrentTenant.Id,
					EmployeeId = employeeId,
					AttendanceDate = date,
					TimeIn = timeIn,
					TimeOut = timeOut,
					StatusType = status,
					EducationMediumStageId = educationMediumStageId
				});
				existingKeys.Add((employeeId, date));
			}
		}

		var insertedCount = await _employeeAttendanceRepository.InsertManyIgnoringDuplicatesAsync(attendancesToInsert);

		_logger.LogInformation(
			"[HRSeeder] Past-year payroll attendance seeded academicYear={AcademicYearName}, range={Start}..{End}, rows={InsertedCount}, employees={EmployeeCount}, holidayDatesSkipped={HolidayDateCount}",
			academicYear.Name,
			windowStart,
			windowEnd,
			insertedCount,
			employeeIds.Count,
			holidayDates.Count);
	}

}
