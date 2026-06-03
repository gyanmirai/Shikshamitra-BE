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
	private async Task EnsureSeededReportingChainAsync()
	{
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var seededEmployees = employeeQuery
			.AsEnumerable()
			.Where(IsSeededDemoEmployee)
			.ToDictionary(e => e.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase);

		await LinkSeededEmployeeToLoginAsync(seededEmployees, "hiren.vadher@ddvs.com", "principal-gseb@ddvs.com");
		await LinkSeededEmployeeToLoginAsync(seededEmployees, "sweta.badgujar@ddvs.com", "teacher-gseb@ddvs.com");
		await LinkSeededEmployeeToLoginAsync(seededEmployees, "punam.badgujar@ddvs.com", "finance-officer@ddvs.com");
		await LinkSeededEmployeeToLoginAsync(seededEmployees, "raj.baku@ddvs.com", "clerk-office@ddvs.com");

		await AssignSeededReportingManagerAsync(seededEmployees, "jignesh.jinjala@ddvs.com", "hiren.vadher@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "akshay.patel@ddvs.com", "jignesh.jinjala@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "parth.ghusar@ddvs.com", "jignesh.jinjala@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "sweta.badgujar@ddvs.com", "akshay.patel@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "jeet.ram@ddvs.com", "akshay.patel@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "dharmik.jinjala@ddvs.com", "akshay.patel@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "jay.dalia@ddvs.com", "parth.ghusar@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "hiren.akbari@ddvs.com", "parth.ghusar@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "prerna.atre@ddvs.com", "parth.ghusar@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "vishal.makwana@ddvs.com", "jignesh.jinjala@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "mayur.solanki@ddvs.com", "vishal.makwana@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "punam.badgujar@ddvs.com", "vishal.makwana@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "milind.kumar@ddvs.com", "vishal.makwana@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "nikhil.baku@ddvs.com", "mayur.solanki@ddvs.com");
		await AssignSeededReportingManagerAsync(seededEmployees, "raj.baku@ddvs.com", "mayur.solanki@ddvs.com");

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task LinkSeededEmployeeToLoginAsync(
		IReadOnlyDictionary<string, Employee> seededEmployees,
		string employeeEmail,
		string loginEmail)
	{
		if (!seededEmployees.TryGetValue(employeeEmail, out var employee))
		{
			return;
		}

		var user = await _userRepository.FindByNormalizedEmailAsync(_lookupNormalizer.NormalizeEmail(loginEmail));
		if (user == null || employee.UserId == user.Id)
		{
			return;
		}

		employee.UpdateUserId(user.Id);
	}

	private async Task AssignSeededReportingManagerAsync(
		IReadOnlyDictionary<string, Employee> seededEmployees,
		string employeeEmail,
		string managerEmail)
	{
		if (!seededEmployees.TryGetValue(employeeEmail, out var employee) ||
		    !seededEmployees.TryGetValue(managerEmail, out var manager) ||
		    employee.ReportingManagerId == manager.Id)
		{
			return;
		}

		employee.AssignReportingManager(manager.Id);
	}
}
