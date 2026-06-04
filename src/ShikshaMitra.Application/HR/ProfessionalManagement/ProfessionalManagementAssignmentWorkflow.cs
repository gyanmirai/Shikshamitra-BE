using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementAssignmentWorkflow(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceReviewStage, Guid> stageRepository,
	ProfessionalManagementTemplateWorkflow templateWorkflow,
	PerformanceTemplateManager templateManager,
	PerformanceReviewStageManager stageManager,
	PerformanceReviewAssignmentManager assignmentManager,
	IAsyncQueryableExecuter asyncExecuter) : ApplicationService, ITransientDependency
{
	public async Task<List<PerformanceReviewAssignment>> GetAssignmentsForCycleAsync(Guid cycleId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.CycleId == cycleId));
	}

	public async Task<List<PerformanceReviewAssignment>> GetAssignmentsForCyclesAsync(List<Guid> cycleIds)
	{
		if (cycleIds.Count == 0)
		{
			return [];
		}

		var query = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => cycleIds.Contains(x.CycleId)));
	}

	public async Task<int> AssignEmployeesAsync(ProfessionalReviewCycle cycle, AssignEmployeesRequest input)
	{
		var employees = await GetEligibleEmployeesAsync(input);
		var existing = await GetAssignmentsForCycleAsync(cycle.Id);
		var createdCount = 0;
		foreach (var employee in employees.Where(employee => existing.All(x => x.EmployeeId != employee.Id)))
		{
			var templateId = input.TemplateId ?? await ResolveTemplateForEmployeeAsync(employee, input.StaffCategory);
			var assignment = assignmentManager.CreateAssignment(
				cycle,
				templateId,
				employee.Id,
				employee.ReportingManagerId,
				await FindReviewerByKeywordAsync(employee.Department, "hod", "coordinator", "head"),
				await FindReviewerByKeywordAsync(null, "principal", "vice principal", "management"));
			await assignmentRepository.InsertAsync(assignment, autoSave: true);
			await EnsureStagesAsync(assignment);
			existing.Add(assignment);
			createdCount++;
		}

		return createdCount;
	}

	private async Task EnsureStagesAsync(PerformanceReviewAssignment assignment)
	{
		foreach (var stageType in Enum.GetValues<PerformanceReviewStageType>())
		{
			await stageRepository.InsertAsync(
				stageManager.CreateStage(assignment, stageType),
				autoSave: true);
		}
	}

	private async Task<List<Employee>> GetEligibleEmployeesAsync(AssignEmployeesRequest input)
	{
		var query = await employeeRepository.GetQueryableAsync();
		query = query
			.WhereIf(input.EmployeeIds.Count > 0, x => input.EmployeeIds.Contains(x.Id))
			.WhereIf(input.ActiveEmployeesOnly, x => x.EmployeeStatusType == null || x.EmployeeStatusType == EmployeeStatusType.Active)
			.WhereIf(!string.IsNullOrWhiteSpace(input.Department), x => x.Department != null && x.Department.Contains(input.Department!))
			.WhereIf(!string.IsNullOrWhiteSpace(input.Designation), x => x.Designation != null && x.Designation.Contains(input.Designation!));
		var employees = await asyncExecuter.ToListAsync(query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName));
		return input.StaffCategory.HasValue
			? employees.Where(x => ProfessionalManagementRules.InferStaffCategory(x) == input.StaffCategory.Value).ToList()
			: employees;
	}

	private async Task<Guid> ResolveTemplateForEmployeeAsync(Employee employee, PerformanceStaffCategory? requestedCategory)
	{
		var staffCategory = requestedCategory ?? ProfessionalManagementRules.InferStaffCategory(employee);
		var query = await templateRepository.GetQueryableAsync();
		var template = await asyncExecuter.FirstOrDefaultAsync(
			query
				.Where(x => x.Status == PerformanceTemplateStatus.Published && x.StaffCategory == staffCategory)
				.OrderBy(x => x.CreationTime));
		template ??= await asyncExecuter.FirstOrDefaultAsync(query.OrderBy(x => x.CreationTime));
		if (template != null)
		{
			return template.Id;
		}

		var defaultRequests = DefaultPerformanceTemplateCatalog.GetDefaultTemplateRequests();
		var request = defaultRequests.FirstOrDefault(x => x.StaffCategory == staffCategory) ??
		              defaultRequests.First(x => x.StaffCategory == PerformanceStaffCategory.Admin);
		template = await templateWorkflow.CreateTemplateInternalAsync(request);
		var sections = await templateWorkflow.GetSectionsAsync(template.Id);
		var criteria = await templateWorkflow.GetCriteriaAsync(template.Id);
		templateManager.Publish(template, sections, criteria);
		await templateRepository.UpdateAsync(template, autoSave: true);
		return template.Id;
	}

	private async Task<Guid?> FindReviewerByKeywordAsync(string? preferredDepartment, params string[] keywords)
	{
		var query = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(
			query.Where(x => x.EmployeeStatusType == null || x.EmployeeStatusType == EmployeeStatusType.Active));
		return employees
			.Where(x => string.IsNullOrWhiteSpace(preferredDepartment) ||
			            string.Equals(x.Department, preferredDepartment, StringComparison.OrdinalIgnoreCase))
			.Concat(employees)
			.FirstOrDefault(x => keywords.Any(keyword =>
				(x.Designation?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(x.Role != null && x.Role.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))))?.Id;
	}
}
