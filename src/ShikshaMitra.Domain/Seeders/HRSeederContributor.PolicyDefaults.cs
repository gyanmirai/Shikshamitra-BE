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
	private async Task SeedDefaultHrPoliciesAsync()
	{
		var academicYears = (await _academicYearRepository.GetListAsync())
			.OrderBy(x => x.StartDate)
			.ToList();
		if (academicYears.Count == 0)
		{
			return;
		}

		var demoScenariosEnabled = ShouldSeedDemoPolicyScenarios();
		var currentStartYear = _clock.Now.Year;
		var policyCount = 0;
		var calendarCount = 0;
		var holidayCount = 0;

		foreach (var academicYear in academicYears)
		{
			var holidays = await SeedDemoHolidaysAsync(academicYear);
			holidayCount += holidays.Count;

			var standardCalendar = await EnsureWorkCalendarPolicyAsync(
				academicYear,
				"Standard School Calendar",
				AttendanceWeeklyOffPattern.SaturdaySunday,
				Array.Empty<int>(),
				WorkCalendarHolidaySyncMode.Snapshot);
			await EnsureWorkCalendarHolidaySnapshotsAsync(standardCalendar, holidays);
			calendarCount++;

			var defaultPayroll = await EnsurePayrollPolicyAsync(
				academicYear,
				standardCalendar,
				"Standard Payroll Policy",
				isDefault: true,
				DayCalculationType.WorkingDays,
				manualDays: null,
				Array.Empty<DayCalculationManualMonth>(),
				SaturdayWorkPolicy.FullDay,
				Array.Empty<PayrollHolidayOverride>());
			if (defaultPayroll != null)
			{
				policyCount++;
			}

			var defaultAttendance = await EnsureAttendancePolicyAsync(
				academicYear,
				standardCalendar,
				"Standard Attendance Policy",
				isDefault: true,
				fullDayThresholdPercentage: 75,
				halfDayThresholdPercentage: 50,
				absentThresholdPercentage: 0,
				AttendanceMissingPunchBehavior.RequiresReview,
				manualCorrectionAllowed: true,
				graceTime: 5,
				maxLateAllowedPerMonth: 3,
				AttendancePenaltyType.None,
				penaltyCountThreshold: 0,
				earlyExitEnabled: false,
				earlyExitGraceTime: 0,
				overtimeEnabled: false,
				overtimeRate: null,
				minOvertimeMinutes: null,
				AttendanceHolidayWorkBehavior.CompOff,
				payrollPreviewEnabled: true);
			if (defaultAttendance != null)
			{
				policyCount++;
			}

			var isPastOrCurrentSeedYear =
				academicYear.Name.StartsWith((currentStartYear - 1).ToString(), StringComparison.OrdinalIgnoreCase) ||
				academicYear.Name.StartsWith(currentStartYear.ToString(), StringComparison.OrdinalIgnoreCase);

			if (!demoScenariosEnabled || !isPastOrCurrentSeedYear)
			{
				continue;
			}

			await SeedDemoHrPoliciesForAcademicYearAsync(academicYear, holidays);
		}

		_logger.LogInformation(
			"[HRSeeder] HR policy baseline ensured academicYears={AcademicYearCount}, calendarsTouched={CalendarCount}, policiesTouched={PolicyCount}, demoHolidaysTouched={HolidayCount}, demoScenarios={DemoScenariosEnabled}",
			academicYears.Count,
			calendarCount,
			policyCount,
			holidayCount,
			demoScenariosEnabled);
	}

	private async Task<WorkCalendarPolicy> EnsureWorkCalendarPolicyAsync(
		AcademicYear academicYear,
		string name,
		AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int> customWeeklyOffs,
		WorkCalendarHolidaySyncMode holidaySyncMode)
	{
		var existing = await _workCalendarPolicyRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.Name == name);
		if (existing != null)
		{
			return existing;
		}

		var policy = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			name,
			HrPolicyStatus.Active,
			academicYear.StartDate,
			academicYear.EndDate,
			weeklyOffPattern,
			customWeeklyOffs,
			holidaySyncMode);
		await _workCalendarPolicyRepository.InsertAsync(policy, autoSave: false);
		return policy;
	}

	private async Task EnsureWorkCalendarHolidaySnapshotsAsync(
		WorkCalendarPolicy policy,
		IReadOnlyList<Holiday> centralHolidays,
		Func<Holiday, bool>? includePredicate = null)
	{
		foreach (var holiday in centralHolidays)
		{
			var fromDate = holiday.StartDate;
			var toDateOnly = holiday.EndDate < holiday.StartDate ? holiday.StartDate : holiday.EndDate;
			var existing = await _workCalendarHolidayRepository.FirstOrDefaultAsync(x =>
				x.WorkCalendarPolicyId == policy.Id &&
				x.CalendarHolidayId == holiday.Id &&
				x.FromDate == fromDate &&
				x.ToDate == toDateOnly);
			if (existing != null)
			{
				continue;
			}

			var snapshot = new WorkCalendarHoliday(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				policy.AcademicYearId,
				policy.Id,
				fromDate,
				toDateOnly,
				holiday.HolidayName,
				WorkCalendarHolidaySource.CentralCalendar,
				holiday.Id,
				includePredicate?.Invoke(holiday) ?? true,
				holiday.Description);
			await _workCalendarHolidayRepository.InsertAsync(snapshot, autoSave: false);
		}
	}
}
