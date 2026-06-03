using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task SeedPerformanceManagementDemoDataAsync(IReadOnlyCollection<Guid> activeEmployeeIds)
	{
		var employees = await GetPerformanceSeedEmployeesAsync(activeEmployeeIds);
		if (employees.Count == 0)
		{
			_logger.LogInformation("[HRSeeder] Skipping performance management demo data because no active employees were found.");
			return;
		}

		var teacherTemplate = await EnsurePerformanceTemplateAsync(
			"Teacher Annual Appraisal",
			PerformanceStaffCategory.Teacher,
			new[]
			{
				("Classroom Practice", 45m, new[] { ("Lesson planning and delivery", 40m, true), ("Student engagement", 35m, false), ("Assessment for learning", 25m, true) }),
				("Student Progress", 25m, new[] { ("Remedial support", 45m, false), ("Learning outcomes tracking", 55m, true) }),
				("Professional Conduct", 30m, new[] { ("Parent communication", 40m, false), ("Collaboration and punctuality", 60m, false) })
			});
		var adminTemplate = await EnsurePerformanceTemplateAsync(
			"Admin and Accounts Appraisal",
			PerformanceStaffCategory.Admin,
			new[]
			{
				("Operational Accuracy", 40m, new[] { ("Documentation accuracy", 50m, true), ("Turnaround time", 50m, false) }),
				("Service Quality", 35m, new[] { ("Stakeholder support", 55m, false), ("Issue resolution", 45m, false) }),
				("Compliance", 25m, new[] { ("Policy adherence", 100m, true) })
			});
		var libraryTemplate = await EnsurePerformanceTemplateAsync(
			"Librarian and Lab Assistant Appraisal",
			PerformanceStaffCategory.LibrarianLabAssistant,
			new[]
			{
				("Resource Readiness", 40m, new[] { ("Inventory discipline", 50m, true), ("Lab/library availability", 50m, false) }),
				("Student Support", 35m, new[] { ("Guidance quality", 55m, false), ("Safety and supervision", 45m, true) }),
				("Coordination", 25m, new[] { ("Teacher coordination", 100m, false) })
			});

		var academicYearId = await ResolvePerformanceSeedAcademicYearIdAsync();
		var cycle = await EnsurePerformanceCycleAsync(academicYearId);
		var templateByCategory = new Dictionary<PerformanceStaffCategory, Guid>
		{
			[PerformanceStaffCategory.Teacher] = teacherTemplate.Id,
			[PerformanceStaffCategory.ClassTeacher] = teacherTemplate.Id,
			[PerformanceStaffCategory.Admin] = adminTemplate.Id,
			[PerformanceStaffCategory.Accounts] = adminTemplate.Id,
			[PerformanceStaffCategory.LibrarianLabAssistant] = libraryTemplate.Id
		};

		var selectedEmployees = employees.Take(Math.Min(12, employees.Count)).ToList();
		for (var index = 0; index < selectedEmployees.Count; index++)
		{
			var employee = selectedEmployees[index];
			var category = InferPerformanceSeedStaffCategory(employee);
			var templateId = templateByCategory.GetValueOrDefault(category, teacherTemplate.Id);
			var assignment = await EnsurePerformanceAssignmentAsync(cycle, employee, templateId);
			await EnsurePerformanceReviewSeedDetailsAsync(assignment, templateId, employee, index);
		}
	}

	private async Task<List<Employee>> GetPerformanceSeedEmployeesAsync(IReadOnlyCollection<Guid> activeEmployeeIds)
	{
		var query = await _employeeRepository.GetQueryableAsync();
		return query
			.Where(x => activeEmployeeIds.Contains(x.Id))
			.Where(x => x.EmployeeStatusType == null || x.EmployeeStatusType == EmployeeStatusType.Active)
			.OrderBy(x => x.Department)
			.ThenBy(x => x.FirstName)
			.ThenBy(x => x.LastName)
			.ToList();
	}

	private async Task<Guid?> ResolvePerformanceSeedAcademicYearIdAsync()
	{
		var query = await _academicYearRepository.GetQueryableAsync();
		return query.OrderByDescending(x => x.CreationTime).FirstOrDefault()?.Id;
	}
}
