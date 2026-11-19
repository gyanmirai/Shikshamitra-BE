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

		AssignSeededReportingManager(seededEmployees, "admin-ops@ddvs.com", "admin-ddvs@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "principal-demo-gseb@ddvs.com", "admin-ddvs@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "principal-gseb@ddvs.com", "admin-ddvs@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "principal-cbse@ddvs.com", "admin-ddvs@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "hr-manager@ddvs.com", "admin-ops@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "finance-officer@ddvs.com", "admin-ops@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "clerk-office@ddvs.com", "hr-manager@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "teacher-demo-1@ddvs.com", "principal-demo-gseb@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "teacher-demo-2@ddvs.com", "principal-demo-gseb@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "teacher-gseb@ddvs.com", "principal-gseb@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "hiren.vadher@ddvs.com", "principal-gseb@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "jignesh.jinjala@ddvs.com", "principal-gseb@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "akshay.patel@ddvs.com", "jignesh.jinjala@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "parth.ghusar@ddvs.com", "jignesh.jinjala@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "sweta.badgujar@ddvs.com", "akshay.patel@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "jeet.ram@ddvs.com", "akshay.patel@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "dharmik.jinjala@ddvs.com", "akshay.patel@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "jay.dalia@ddvs.com", "parth.ghusar@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "hiren.akbari@ddvs.com", "parth.ghusar@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "prerna.atre@ddvs.com", "parth.ghusar@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "vishal.makwana@ddvs.com", "jignesh.jinjala@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "mayur.solanki@ddvs.com", "vishal.makwana@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "punam.badgujar@ddvs.com", "vishal.makwana@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "milind.kumar@ddvs.com", "vishal.makwana@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "nikhil.baku@ddvs.com", "mayur.solanki@ddvs.com");
		AssignSeededReportingManager(seededEmployees, "raj.baku@ddvs.com", "mayur.solanki@ddvs.com");

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private void AssignSeededReportingManager(
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
