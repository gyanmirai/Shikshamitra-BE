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
	private async Task SeedSampleAttendanceForEmployeesAsync(List<Guid> employeeIds, Guid educationMediumStageId)
	{
		employeeIds = employeeIds
			.Distinct()
			.ToList();

		if (employeeIds.Count == 0)
		{
			return;
		}

		// Respect the configurable window; 0 means skip attendance seeding entirely (e.g. in tests).
		var monthWindow = PastAttendanceMonthsToSeed();
		if (monthWindow == 0)
		{
			return;
		}

		var today = DateOnly.FromDateTime(_clock.Now);
		var windowStartMonth = new DateTime(_clock.Now.Year, _clock.Now.Month, 1)
			.AddMonths(-(monthWindow - 1));
		var windowStart = DateOnly.FromDateTime(windowStartMonth);
		var holidayDates = await BuildSeedHolidayDateSetAsync(windowStart, today);

		List<EmployeeAttendance> existingRows;
		using (_dataFilter.Disable<ISoftDelete>())
		{
			existingRows = await _employeeAttendanceRepository.GetListAsync(a =>
				employeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate >= windowStart &&
				a.AttendanceDate <= today);
		}
		var existingKeys = existingRows
			.Select(a => (a.EmployeeId, a.AttendanceDate))
			.ToHashSet();

		var indiaOffset = TimeSpan.FromHours(5.5);
		var workingDayIndex = 0;
		var attendancesToInsert = new List<EmployeeAttendance>();

		for (var date = windowStart; date <= today; date = date.AddDays(1))
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

				var mix = (employeeId.GetHashCode() ^ date.DayOfYear) & 0x7;
				EmployeeAttendanceStatusType status;
				DateTimeOffset? timeIn = null;
				DateTimeOffset? timeOut = null;

				if (workingDayIndex % 9 == 0)
				{
					status = EmployeeAttendanceStatusType.FullDayLeave;
				}
				else if (workingDayIndex % 5 == 0 || mix == 0)
				{
					status = EmployeeAttendanceStatusType.HalfDayLeave;
					timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, indiaOffset);
					timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, 13, 30, 0, indiaOffset);
				}
				else
				{
					status = EmployeeAttendanceStatusType.Present;
					timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, indiaOffset);
					timeOut = new DateTimeOffset(date.Year, date.Month, date.Day, 17, 30, 0, indiaOffset);
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
			"[HRSeeder] Attendance sample seeded rows={InsertedCount} for employees={EmployeeCount}, holidayDatesSkipped={HolidayDateCount}",
			insertedCount,
			employeeIds.Count,
			holidayDates.Count);
	}
}
