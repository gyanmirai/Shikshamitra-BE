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
	private async Task SeedDemoHrPoliciesForAcademicYearAsync(AcademicYear academicYear, IReadOnlyList<Holiday> holidays)
	{
		var operationsCalendar = await EnsureWorkCalendarPolicyAsync(
			academicYear,
			"Operations Six-Day Calendar",
			AttendanceWeeklyOffPattern.Sunday,
			Array.Empty<int>(),
			WorkCalendarHolidaySyncMode.Snapshot);
		var partTimeCalendar = await EnsureWorkCalendarPolicyAsync(
			academicYear,
			"Part-Time Academic Calendar",
			AttendanceWeeklyOffPattern.Custom,
			new[] { 0, 2, 4, 6 },
			WorkCalendarHolidaySyncMode.Snapshot);
		await EnsureWorkCalendarHolidaySnapshotsAsync(
			operationsCalendar,
			holidays,
			holiday => !string.Equals(holiday.HolidayName, "Local Declared Holiday", StringComparison.OrdinalIgnoreCase));
		await EnsureWorkCalendarHolidaySnapshotsAsync(
			partTimeCalendar,
			holidays,
			holiday => holiday.HolidayName is "Founders Day" or "Diwali Break" or "Christmas");

		var firstHoliday = holidays.OrderBy(x => x.StartDate).FirstOrDefault();
		var secondHoliday = holidays.OrderBy(x => x.StartDate).Skip(1).FirstOrDefault();
		var operationsOverrides = new List<PayrollHolidayOverride>();
		if (firstHoliday != null)
		{
			operationsOverrides.Add(new PayrollHolidayOverride(
				firstHoliday.StartDate.Date,
				firstHoliday.HolidayName,
				IncludedInPayroll: true,
				firstHoliday.Id));
		}

		if (secondHoliday != null)
		{
			operationsOverrides.Add(new PayrollHolidayOverride(
				secondHoliday.StartDate.Date,
				secondHoliday.HolidayName,
				IncludedInPayroll: false,
				secondHoliday.Id));
		}

		await EnsurePayrollPolicyAsync(
			academicYear,
			operationsCalendar,
			"Operations Payroll Policy",
			isDefault: false,
			DayCalculationType.WorkingDays,
			manualDays: null,
			Array.Empty<DayCalculationManualMonth>(),
			SaturdayWorkPolicy.FullDay,
			operationsOverrides);
		await EnsurePayrollPolicyAsync(
			academicYear,
			partTimeCalendar,
			"Part-Time Payroll Policy",
			isDefault: false,
			DayCalculationType.ManualDays,
			manualDays: 14,
			Array.Empty<DayCalculationManualMonth>(),
			SaturdayWorkPolicy.Off,
			Array.Empty<PayrollHolidayOverride>());

		await EnsureAttendancePolicyAsync(
			academicYear,
			operationsCalendar,
			"Operations Attendance Policy",
			isDefault: false,
			fullDayThresholdPercentage: 80,
			halfDayThresholdPercentage: 45,
			absentThresholdPercentage: 0,
			AttendanceMissingPunchBehavior.RequiresReview,
			manualCorrectionAllowed: true,
			graceTime: 10,
			maxLateAllowedPerMonth: 2,
			AttendancePenaltyType.DeductHalfDay,
			penaltyCountThreshold: 3,
			earlyExitEnabled: true,
			earlyExitGraceTime: 10,
			overtimeEnabled: true,
			AttendanceOvertimeRate.OnePointFiveX,
			minOvertimeMinutes: 30,
			AttendanceHolidayWorkBehavior.OvertimePayout,
			sandwichRuleEnabled: true,
			payrollPreviewEnabled: true);
		await EnsureAttendancePolicyAsync(
			academicYear,
			partTimeCalendar,
			"Part-Time Attendance Policy",
			isDefault: false,
			fullDayThresholdPercentage: 70,
			halfDayThresholdPercentage: 40,
			absentThresholdPercentage: 0,
			AttendanceMissingPunchBehavior.MarkHalfDay,
			manualCorrectionAllowed: true,
			graceTime: 15,
			maxLateAllowedPerMonth: 4,
			AttendancePenaltyType.None,
			penaltyCountThreshold: 0,
			earlyExitEnabled: true,
			earlyExitGraceTime: 15,
			overtimeEnabled: false,
			overtimeRate: null,
			minOvertimeMinutes: null,
			AttendanceHolidayWorkBehavior.Paid,
			sandwichRuleEnabled: false,
			payrollPreviewEnabled: true);
	}

	private async Task<IReadOnlyList<Holiday>> SeedDemoHolidaysAsync(AcademicYear academicYear)
	{
		var type = await _holidayTypeRepository.FirstOrDefaultAsync(x => x.Name == "HR Demo Holiday");
		if (type == null)
		{
			type = HolidayType.Create(GuidGenerator.Create(), "HR Demo Holiday", "#F59E0B", "Seeded HR holiday scenarios", CurrentTenant.Id);
			await _holidayTypeRepository.InsertAsync(type, autoSave: true);
		}

		var templates = new[]
		{
			new { Name = "Founders Day", StartDate = academicYear.StartDate.AddMonths(1).AddDays(14), EndDate = academicYear.StartDate.AddMonths(1).AddDays(14), Description = "Seeded paid school holiday." },
			new { Name = "Local Declared Holiday", StartDate = academicYear.StartDate.AddMonths(3).AddDays(9), EndDate = academicYear.StartDate.AddMonths(3).AddDays(9), Description = "Seeded local holiday used for payroll override testing." },
			new { Name = "Annual Function Workday", StartDate = academicYear.StartDate.AddMonths(5).AddDays(4), EndDate = academicYear.StartDate.AddMonths(5).AddDays(4), Description = "Seeded holiday-work scenario for attendance impact testing." },
			new { Name = "Diwali Break", StartDate = academicYear.StartDate.AddMonths(6).AddDays(10), EndDate = academicYear.StartDate.AddMonths(6).AddDays(14), Description = "Seeded multi-day festival break." },
			new { Name = "Summer Vacation", StartDate = academicYear.EndDate.AddMonths(-1).Date, EndDate = academicYear.EndDate.Date, Description = "Seeded long vacation range for work calendar testing." },
			new { Name = "Christmas", StartDate = new DateTime(academicYear.StartDate.Year, 12, 25), EndDate = new DateTime(academicYear.StartDate.Year, 12, 25), Description = "Seeded national/festival holiday." },
			new { Name = "Republic Day", StartDate = new DateTime(academicYear.EndDate.Year, 1, 26), EndDate = new DateTime(academicYear.EndDate.Year, 1, 26), Description = "Seeded national holiday." },
			new { Name = "Independence Day", StartDate = new DateTime(academicYear.StartDate.Year, 8, 15), EndDate = new DateTime(academicYear.StartDate.Year, 8, 15), Description = "Seeded national holiday." }
		};

		var holidays = new List<Holiday>();
		foreach (var template in templates)
		{
			var existing = await _holidayRepository.FirstOrDefaultAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.HolidayName == template.Name);
			if (existing != null)
			{
				holidays.Add(existing);
				continue;
			}

			var holiday = Holiday.Create(
				CurrentTenant.Id,
				academicYear.Id,
				template.Name,
				type.Id,
				new DateTimeOffset(DateTime.SpecifyKind(template.StartDate.Date, DateTimeKind.Unspecified), TimeSpan.Zero),
				new DateTimeOffset(DateTime.SpecifyKind(template.EndDate.Date, DateTimeKind.Unspecified), TimeSpan.Zero),
				template.Description);
			await _holidayRepository.InsertAsync(holiday, autoSave: false);
			holidays.Add(holiday);
		}

		return holidays;
	}
}
