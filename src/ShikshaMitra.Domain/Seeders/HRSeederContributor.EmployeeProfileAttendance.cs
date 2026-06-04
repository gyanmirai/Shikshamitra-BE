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
	internal static List<WorkExperience> BuildWorkExperiences(SuratSeedEmployeeProfile profile)
	{
		var joinYear = profile.DateOfJoining.Year;
		var experiences = new List<WorkExperience>
		{
			new(
				"Surat Public School",
				"Adajan, Surat",
				new DateOnly(Math.Max(2005, joinYear - 6), 6, 1),
				new DateOnly(Math.Max(2006, joinYear - 3), 4, 30),
				profile.Department,
				profile.Designation.Contains("Principal", StringComparison.OrdinalIgnoreCase)
					? "Academic Coordinator"
					: profile.EmployeeGroup == EmployeeGroupType.Teaching
						? $"Assistant {profile.Designation}"
						: $"Assistant {profile.Department}",
				"Handled classroom operations, parent communication, compliance records, and academic planning.",
				"Available with HR")
		};

		if (profile.Designation.Contains("Principal", StringComparison.OrdinalIgnoreCase) ||
		    profile.Designation.Contains("Head", StringComparison.OrdinalIgnoreCase) ||
		    profile.Designation.Contains("Manager", StringComparison.OrdinalIgnoreCase))
		{
			experiences.Add(new WorkExperience(
				"Tapti Valley Academy",
				"Vesu, Surat",
				new DateOnly(Math.Max(2008, joinYear - 3), 6, 1),
				new DateOnly(joinYear, 5, 31),
				profile.Department,
				profile.Designation.Contains("Principal", StringComparison.OrdinalIgnoreCase)
					? "Vice Principal"
					: profile.Designation,
				"Led staff planning, timetable coordination, attendance reviews, and policy implementation.",
				"Available with HR"));
		}
		else if (profile.EmployeeGroup == EmployeeGroupType.Teaching)
		{
			experiences.Add(new WorkExperience(
				"Rander Modern School",
				"Rander, Surat",
				new DateOnly(Math.Max(2010, joinYear - 2), 6, 1),
				new DateOnly(joinYear, 5, 31),
				profile.Department,
				profile.Designation,
				"Prepared lesson plans, assessments, remedial classes, and student progress reports.",
				"Available with HR"));
		}

		return experiences;
	}

	private async Task SeedPreviousCompletedMonthAttendanceAsync(List<Guid> employeeIds, Guid educationMediumStageId)
	{
		employeeIds = employeeIds.Distinct().ToList();
		if (employeeIds.Count == 0)
		{
			return;
		}

		// Skip attendance seeding when months = 0 (e.g. in integration tests).
		if (PastAttendanceMonthsToSeed() == 0)
		{
			return;
		}

		var today = _clock.Now.Date;
		var previousMonthStartDate = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
		var previousMonthEndDate = previousMonthStartDate.AddMonths(1).AddDays(-1);
		var windowStart = DateOnly.FromDateTime(previousMonthStartDate);
		var windowEnd = DateOnly.FromDateTime(previousMonthEndDate);
		var holidayDates = await BuildSeedHolidayDateSetAsync(windowStart, windowEnd);

		List<EmployeeAttendance> existingRows;
		using (_dataFilter.Disable<ISoftDelete>())
		{
			existingRows = await _employeeAttendanceRepository.GetListAsync(a =>
				employeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate >= windowStart &&
				a.AttendanceDate <= windowEnd);
		}

		var existingKeys = existingRows.Select(a => (a.EmployeeId, a.AttendanceDate)).ToHashSet();
		var indiaOffset = TimeSpan.FromHours(5.5);
		var rows = new List<EmployeeAttendance>();
		for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
		{
			if (!ShouldSeedAttendanceForDate(date, holidayDates))
			{
				continue;
			}

			foreach (var employeeId in employeeIds)
			{
				if (existingKeys.Contains((employeeId, date)))
				{
					continue;
				}

				var seed = StableSeedHash($"{employeeId:N}:{date:yyyyMMdd}:previous-month");
				var status = seed % 17 == 0
					? EmployeeAttendanceStatusType.FullDayLeave
					: seed % 11 == 0
						? EmployeeAttendanceStatusType.HalfDayLeave
						: EmployeeAttendanceStatusType.Present;
				DateTimeOffset? timeIn = null;
				DateTimeOffset? timeOut = null;
				if (status != EmployeeAttendanceStatusType.FullDayLeave)
				{
					var lateMinutes = seed % 13 == 0 ? 28 : seed % 5 == 0 ? 9 : 0;
					timeIn = new DateTimeOffset(date.Year, date.Month, date.Day, 8, 0 + lateMinutes, 0, indiaOffset);
					timeOut = status == EmployeeAttendanceStatusType.HalfDayLeave
						? new DateTimeOffset(date.Year, date.Month, date.Day, 13, 0, 0, indiaOffset)
						: new DateTimeOffset(date.Year, date.Month, date.Day, 16, seed % 19 == 0 ? 50 : 30, 0, indiaOffset);
				}

				if (seed % 23 == 0 && status == EmployeeAttendanceStatusType.Present)
				{
					timeOut = null;
				}

				rows.Add(new EmployeeAttendance
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

		var inserted = await _employeeAttendanceRepository.InsertManyIgnoringDuplicatesAsync(rows);
		_logger.LogInformation(
			"[HRSeeder] Previous completed month attendance ensured range={Start}..{End}, rows={InsertedCount}, employees={EmployeeCount}, holidayDatesSkipped={HolidayDateCount}",
			windowStart,
			windowEnd,
			inserted,
			employeeIds.Count,
			holidayDates.Count);
	}
}
