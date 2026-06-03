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
	private async Task<List<Guid>> EnsureEmployeeStatusDistributionAsync(List<Guid> seededEmployeeIds)
	{
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		if (seededEmployeeIds.Count > 0)
		{
			employeeQuery = employeeQuery.Where(e => seededEmployeeIds.Contains(e.Id));
		}

		var employees = employeeQuery
			.Where(e =>
				!e.EmployeeStatusType.HasValue ||
				e.EmployeeStatusType == EmployeeStatusType.Active ||
				e.EmployeeStatusType == EmployeeStatusType.Inactive)
			.AsEnumerable()
			.Where(IsSeededDemoEmployee)
			.OrderBy(e => e.EmployeeCode)
			.ToList();
		if (employees.Count == 0)
		{
			return new List<Guid>();
		}

		var inactiveCount = employees.Count <= 1
			? 0
			: (int)Math.Round(
				employees.Count * ((100m - ActiveEmployeeSeedPercentage) / 100m),
				MidpointRounding.AwayFromZero);
		inactiveCount = Math.Clamp(inactiveCount, 0, employees.Count - 1);

		var inactiveEmployeeIds = employees
			.OrderBy(e => StableSeedHash($"{CurrentTenant.Id:N}:{e.EmployeeCode}:{e.Email}:{e.Id:N}"))
			.Take(inactiveCount)
			.Select(e => e.Id)
			.ToHashSet();

		var activeEmployeeIds = new List<Guid>();
		var updatedCount = 0;
		foreach (var employee in employees)
		{
			var targetStatus = inactiveEmployeeIds.Contains(employee.Id)
				? EmployeeStatusType.Inactive
				: EmployeeStatusType.Active;
			if (targetStatus == EmployeeStatusType.Active)
			{
				activeEmployeeIds.Add(employee.Id);
			}

			if (employee.EmployeeStatusType == targetStatus)
			{
				continue;
			}

			var updated = await _employeeManager.UpdateEmployeeStatusAsync(
				employee.Id,
				targetStatus,
				$"Set by HR seeder to maintain {ActiveEmployeeSeedPercentage}% active demo employee distribution");
			updatedCount++;
		}

		if (updatedCount > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation(
			"[HRSeeder] Employee status distribution applied active={ActiveCount}, inactive={InactiveCount}, updated={UpdatedCount}",
			activeEmployeeIds.Count,
			inactiveCount,
			updatedCount);

		return activeEmployeeIds;
	}
}
