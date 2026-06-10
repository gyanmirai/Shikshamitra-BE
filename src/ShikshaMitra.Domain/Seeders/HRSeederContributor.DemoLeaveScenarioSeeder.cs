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
	private async Task SeedSampleLeaveScenariosAsync(List<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return;
		}

		var currentYear = _clock.Now.Year;
		var academicYear = await _academicYearRepository
			.FirstOrDefaultAsync(y => y.Name.StartsWith(currentYear.ToString()))
			?? await _academicYearRepository.FirstOrDefaultAsync();
		if (academicYear == null)
		{
			return;
		}

		var leavePolicy = (await _leavePolicyRepository.GetListAsync(
				x => x.AcademicYearId == academicYear.Id,
				includeDetails: true))
			.FirstOrDefault(x => x.PolicyName.Contains("Teaching", StringComparison.OrdinalIgnoreCase));
		var casual = leavePolicy?.Rules.FirstOrDefault(x => x.LeaveType?.Type.Contains("Casual", StringComparison.OrdinalIgnoreCase) == true);
		var sick = leavePolicy?.Rules.FirstOrDefault(x => x.LeaveType?.Type.Contains("Sick", StringComparison.OrdinalIgnoreCase) == true);
		var lossOfPay = leavePolicy?.Rules.FirstOrDefault(x => x.LeaveType?.Type.Contains("Loss", StringComparison.OrdinalIgnoreCase) == true);
		if (leavePolicy == null || casual == null || sick == null)
		{
			return;
		}

		var currentMonthStart = new DateTime(_clock.Now.Year, _clock.Now.Month, 1);
		var candidates = employeeIds.Take(Math.Min(4, employeeIds.Count)).ToList();
		var insertedCount = 0;
		var scenarios = new List<LeaveSeedScenario>
		{
			new("HR seed approved sick leave", EmployeeLeaveStatusType.Approved, leavePolicy, sick, 2),
			new("HR seed pending casual leave", EmployeeLeaveStatusType.Pending, leavePolicy, casual, 5),
			new("HR seed rejected casual leave", EmployeeLeaveStatusType.Rejected, leavePolicy, casual, 8, "Seeded rejection scenario."),
			new("HR seed approved loss of pay leave", EmployeeLeaveStatusType.Approved, leavePolicy, lossOfPay ?? casual, 11)
		};

		for (var i = 0; i < candidates.Count && i < scenarios.Count; i++)
		{
			var employeeId = candidates[i];
			var scenario = scenarios[i];
			var leaveDate = GetCurrentMonthBusinessDayNotFuture(currentMonthStart, scenario.BusinessDay);
			if (await _employeeLeaveRepository.AnyAsync(x =>
				    x.EmployeeId == employeeId &&
				    x.Description == scenario.Description))
			{
				continue;
			}

			var leave = await _employeeLeaveManager.CreateAsync(
				employeeId,
				scenario.Policy.Id,
				scenario.Rule.Id,
				scenario.Rule.LeaveTypeId,
				new DateTimeOffset(leaveDate, TimeSpan.FromHours(5.5)),
				new DateTimeOffset(leaveDate, TimeSpan.FromHours(5.5)),
				scenario.Description);
			await _employeeLeaveRepository.InsertAsync(leave, autoSave: true);
			if (scenario.Status != EmployeeLeaveStatusType.Pending)
			{
				await _employeeLeaveManager.UpdateLeaveRequestStatusAsync(
					leave,
					scenario.Status,
					scenario.RejectionReason);
			}
			insertedCount++;
		}

		_logger.LogInformation(
			"[HRSeeder] Leave scenario sample seeded rows={InsertedCount}",
			insertedCount);
	}

	private sealed record LeaveSeedScenario(
		string Description,
		EmployeeLeaveStatusType Status,
		LeavePolicy Policy,
		LeavePolicyRule Rule,
		int BusinessDay,
		string? RejectionReason = null);

	private DateTime GetCurrentMonthBusinessDayNotFuture(DateTime monthStart, int preferredBusinessDayNumber)
	{
		var today = _clock.Now.Date;
		var count = 0;
		var selected = monthStart.Date;
		for (var date = monthStart.Date; date <= today; date = date.AddDays(1))
		{
			if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
			{
				continue;
			}

			count++;
			selected = date;
			if (count == preferredBusinessDayNumber)
			{
				return date;
			}
		}

		return selected;
	}

}
