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
	private async Task<List<Guid>> SeedSampleEmployeePayrollAndAttendanceAsync(List<Guid> activeEmployeeIds)
	{
		var educationMediumStage = (await _educationMediumStageRepository.GetQueryableAsync()).FirstOrDefault();
		if (educationMediumStage == null)
		{
			return new List<Guid>();
		}

		if (!await _earningComponentRepository.AnyAsync() || !await _deductionComponentRepository.AnyAsync())
		{
			return new List<Guid>();
		}

		var employeeIds = await ResolveActivePayrollEmployeeIdsAsync(activeEmployeeIds);
		if (employeeIds.Count == 0)
		{
			return employeeIds;
		}

		foreach (var employeeId in employeeIds)
		{
			await SeedSamplePayrollForEmployeeIfNeededAsync(employeeId);
		}

		await SeedSampleAttendanceForEmployeesAsync(
			employeeIds,
			educationMediumStage.Id);
		await SeedPreviousCompletedMonthAttendanceAsync(
			employeeIds,
			educationMediumStage.Id);
		await SeedPastAcademicYearPayrollAttendanceAsync(
			employeeIds,
			educationMediumStage.Id);

		return employeeIds;
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
