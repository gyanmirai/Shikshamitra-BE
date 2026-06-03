using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<PerformanceTemplate> EnsurePerformanceTemplateAsync(
		string name,
		PerformanceStaffCategory staffCategory,
		(string Name, decimal Weight, (string Name, decimal Weight, bool EvidenceRequired)[] Criteria)[] sectionSpecs)
	{
		var templateQuery = await _performanceTemplateRepository.GetQueryableAsync();
		var template = templateQuery.FirstOrDefault(x => x.Name == name);
		if (template == null)
		{
			template = new PerformanceTemplate(GuidGenerator.Create(), CurrentTenant.Id, name, staffCategory, null, "Seeded demo appraisal template");
			await _performanceTemplateRepository.InsertAsync(template, autoSave: true);
		}

		var sectionQuery = await _performanceTemplateSectionRepository.GetQueryableAsync();
		var existingSections = sectionQuery.Where(x => x.TemplateId == template.Id).ToList();
		if (existingSections.Count == 0)
		{
			var displayOrder = 1;
			foreach (var sectionSpec in sectionSpecs)
			{
				var section = new PerformanceTemplateSection(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					template.Id,
					sectionSpec.Name,
					sectionSpec.Weight,
					displayOrder++);
				await _performanceTemplateSectionRepository.InsertAsync(section, autoSave: true);

				var criterionOrder = 1;
				foreach (var criterionSpec in sectionSpec.Criteria)
				{
					await _performanceTemplateCriterionRepository.InsertAsync(
						new PerformanceTemplateCriterion(
							GuidGenerator.Create(),
							CurrentTenant.Id,
							template.Id,
							section.Id,
							criterionSpec.Name,
							null,
							criterionSpec.Weight,
							criterionSpec.EvidenceRequired,
							criterionOrder++),
						autoSave: true);
				}
			}
		}

		if (template.Status != PerformanceTemplateStatus.Published)
		{
			var sections = (await _performanceTemplateSectionRepository.GetQueryableAsync()).Where(x => x.TemplateId == template.Id).ToList();
			var criteria = (await _performanceTemplateCriterionRepository.GetQueryableAsync()).Where(x => x.TemplateId == template.Id).ToList();
			template.Publish(sections.Sum(x => x.Weight), sections.All(section => criteria.Any(criterion => criterion.SectionId == section.Id)));
			await _performanceTemplateRepository.UpdateAsync(template, autoSave: true);
		}

		return template;
	}

	private async Task<ProfessionalReviewCycle> EnsurePerformanceCycleAsync(Guid? academicYearId)
	{
		const string cycleName = "Annual Staff Appraisal Demo";
		var cycleQuery = await _performanceCycleRepository.GetQueryableAsync();
		var cycle = cycleQuery.FirstOrDefault(x => x.Name == cycleName);
		if (cycle != null)
		{
			return cycle;
		}

		var today = _clock.Now.Date;
		cycle = new ProfessionalReviewCycle(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			cycleName,
			PerformanceCycleType.Annual,
			new DateTime(today.Year, 4, 1),
			new DateTime(today.Year + 1, 3, 31),
			today.AddDays(-7),
			today.AddDays(7),
			today.AddDays(21),
			5,
			true,
			"{\"demo\":true}",
			"{\"stages\":[\"SelfAppraisal\",\"ReportingManager\",\"HodCoordinator\",\"PrincipalManagement\",\"HrVerification\"]}",
			ProfessionalReviewCycleStatus.InProgress);
		await _performanceCycleRepository.InsertAsync(cycle, autoSave: true);
		return cycle;
	}

	private async Task<PerformanceReviewAssignment> EnsurePerformanceAssignmentAsync(
		ProfessionalReviewCycle cycle,
		Employee employee,
		Guid templateId)
	{
		var assignmentQuery = await _performanceAssignmentRepository.GetQueryableAsync();
		var assignment = assignmentQuery.FirstOrDefault(x => x.CycleId == cycle.Id && x.EmployeeId == employee.Id);
		if (assignment != null)
		{
			return assignment;
		}

		assignment = new PerformanceReviewAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			cycle.Id,
			templateId,
			employee.Id,
			employee.ReportingManagerId,
			await FindPerformanceSeedReviewerByKeywordAsync(employee.Department, "hod", "coordinator", "head"),
			await FindPerformanceSeedReviewerByKeywordAsync(null, "principal", "vice principal", "management"));
		await _performanceAssignmentRepository.InsertAsync(assignment, autoSave: true);
		return assignment;
	}

	private async Task<Guid?> FindPerformanceSeedReviewerByKeywordAsync(string? preferredDepartment, params string[] keywords)
	{
		var query = await _employeeRepository.GetQueryableAsync();
		var employees = query
			.Where(x => x.EmployeeStatusType == null || x.EmployeeStatusType == EmployeeStatusType.Active)
			.ToList();
		return employees
			.Where(x => string.IsNullOrWhiteSpace(preferredDepartment) || string.Equals(x.Department, preferredDepartment, StringComparison.OrdinalIgnoreCase))
			.Concat(employees)
			.FirstOrDefault(x => keywords.Any(keyword =>
				(x.Designation?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(x.Role != null && x.Role.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))))?.Id;
	}
}
