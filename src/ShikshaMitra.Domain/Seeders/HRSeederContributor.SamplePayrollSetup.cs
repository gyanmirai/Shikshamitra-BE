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
	internal async Task EnsureFullDemoEmployeeStatusAndCompensationAsync()
	{
		var seededEmails = BuildSuratSeedEmployeeProfiles().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var employees = (await _employeeRepository.GetListAsync())
			.Where(x =>
				!string.IsNullOrWhiteSpace(x.Email) &&
				seededEmails.Contains(x.Email))
			.OrderBy(x => x.EmployeeCode)
			.ToList();
		var activeEmployeeIds = await EnsureEmployeeStatusDistributionAsync(
			employees.Select(x => x.Id).ToList());
		var pfRuleId = await ResolveSeedProvidentFundRuleIdAsync();
		foreach (var employeeId in activeEmployeeIds)
		{
			await SeedSamplePayrollForEmployeeIfNeededAsync(employeeId, pfRuleId);
		}
	}

	internal async Task EnsureFullDemoInactiveEmployeeExitDatesAsync()
	{
		var seededEmails = BuildSuratSeedEmployeeProfiles().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var inactiveEmployees = (await _employeeRepository.GetListAsync())
			.Where(x =>
				x.EmployeeStatusType == EmployeeStatusType.Inactive &&
				!string.IsNullOrWhiteSpace(x.Email) &&
				seededEmails.Contains(x.Email))
			.ToList();
		var exitedOn = DateOnly.FromDateTime(EsiFullDemoSeedCatalog.RuleEffectiveFrom).AddDays(-1);
		var updatedCount = 0;
		foreach (var employee in inactiveEmployees)
		{
			if (employee.StatusEffectiveDate == exitedOn)
			{
				continue;
			}

			await _employeeManager.UpdateEmployeeStatusAsync(
				employee.Id,
				EmployeeStatusType.Inactive,
				"Synthetic FullDemo employee exited before ESI demo coverage",
				exitedOn);
			updatedCount++;
		}

		if (updatedCount > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	private async Task<List<Guid>> SeedSampleEmployeePayrollAndAttendanceAsync(List<Guid> activeEmployeeIds)
	{
		var payrollEmployeeIds = await ResolvePayrollStructureEmployeeIdsAsync(activeEmployeeIds);
		var attendanceEmployeeIds = await ResolveActivePayrollEmployeeIdsAsync(activeEmployeeIds);

		if (payrollEmployeeIds.Count > 0)
		{
			var pfRuleId = await ResolveSeedProvidentFundRuleIdAsync();
			foreach (var employeeId in payrollEmployeeIds)
			{
				await SeedSamplePayrollForEmployeeIfNeededAsync(employeeId, pfRuleId);
			}
		}

		if (attendanceEmployeeIds.Count == 0)
		{
			return attendanceEmployeeIds;
		}

		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
		if (educationMediumStage == null)
		{
			return attendanceEmployeeIds;
		}

		await SeedSampleAttendanceForEmployeesAsync(
			attendanceEmployeeIds,
			educationMediumStage.Id);
		await SeedPreviousCompletedMonthAttendanceAsync(
			attendanceEmployeeIds,
			educationMediumStage.Id);
		await SeedPastAcademicYearPayrollAttendanceAsync(
			attendanceEmployeeIds,
			educationMediumStage.Id);

		return attendanceEmployeeIds;
	}

	private async Task<List<Guid>> ResolvePayrollStructureEmployeeIdsAsync(List<Guid> activeEmployeeIds)
	{
		var employeeIds = activeEmployeeIds
			.Distinct()
			.ToList();
		var seedEmails = BuildSuratSeedEmployeeProfiles().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var seededProfileEmployeeIds = employeeQuery
			.AsEnumerable()
			.Where(e => e.Email != null && seedEmails.Contains(e.Email))
			.Select(e => e.Id)
			.ToList();

		employeeIds.AddRange(seededProfileEmployeeIds);
		employeeIds = employeeIds
			.Distinct()
			.ToList();
		if (employeeIds.Count > 0)
		{
			return employeeIds;
		}

		return employeeQuery
			.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
			.OrderBy(e => e.EmployeeCode)
			.Select(e => e.Id)
			.ToList();
	}

	private async Task<List<Guid>> ResolveActivePayrollEmployeeIdsAsync(List<Guid> activeEmployeeIds)
	{
		var employeeIds = activeEmployeeIds
			.Distinct()
			.ToList();

		if (employeeIds.Count > 0)
		{
			return employeeIds;
		}

		var existingEmployeeQuery = await _employeeRepository.GetQueryableAsync();
		return existingEmployeeQuery
			.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
			.OrderBy(e => e.EmployeeCode)
			.Select(e => e.Id)
			.ToList();
	}

	private async Task<string?> ResolveSeedProvidentFundRuleIdAsync()
	{
		var providentFundQuery = await _providentFundRepository.GetQueryableAsync();
		var providentFundId = providentFundQuery
			.Where(x => x.Code == "STD-PF")
			.Select(x => x.Id)
			.FirstOrDefault();

		return providentFundId == Guid.Empty ? null : providentFundId.ToString();
	}

	private async Task ActivateEmployeesWithPayrollStructureAsync(List<Guid> employeeIds)
	{
		foreach (var employeeId in employeeIds)
		{
			var employee = await _employeeRepository.FindAsync(employeeId);
			if (employee == null || employee.EmployeeStatusType == EmployeeStatusType.Active)
			{
				continue;
			}

			var payrollComponents = await _employeePayrollComponentRepository.GetByEmployeeIdAsync(employeeId);
			if (payrollComponents.Count == 0)
			{
				continue;
			}

			var updated = await _employeeManager.UpdateEmployeeStatusAsync(
				employeeId,
				EmployeeStatusType.Active,
				"Auto-activated by HR seeder after payroll structure seed");

			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}
}
