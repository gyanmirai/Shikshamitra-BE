using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementDashboardLookupReader(
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<ProfessionalReviewDto>> GetReviewsAsync(
		Guid? cycleId,
		Guid? employeeId,
		ProfessionalReviewStatus? status,
		string? search)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		if (cycleId.HasValue)
		{
			query = query.Where(x => x.CycleId == cycleId);
		}

		if (employeeId.HasValue)
		{
			query = query.Where(x => x.EmployeeId == employeeId);
		}

		if (status.HasValue)
		{
			query = query.Where(x => x.Status == status);
		}

		var assignments = await asyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime));
		var cycles = await GetCyclesByIdsAsync(assignments.Select(x => x.CycleId).Distinct().ToList());
		var employees = await GetEmployeesByIdsAsync(assignments.Select(x => x.EmployeeId).Distinct().ToList());
		var templates = await GetTemplatesByIdsAsync(assignments.Select(x => x.TemplateId).Distinct().ToList());
		var items = assignments
			.Select(x => ProfessionalManagementProjection.ToReviewDto(
				x,
				cycles.GetValueOrDefault(x.CycleId),
				employees.GetValueOrDefault(x.EmployeeId),
				templates.GetValueOrDefault(x.TemplateId)))
			.ToList();

		if (!string.IsNullOrWhiteSpace(search))
		{
			items = items
				.Where(x =>
					x.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					(x.EmployeeIdentifier?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
				.ToList();
		}

		return items;
	}

	public async Task<List<PerformanceReviewAssignment>> GetAssignmentsForCycleAsync(Guid cycleId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.CycleId == cycleId));
	}

	public async Task<Dictionary<Guid, ProfessionalReviewCycle>> GetCyclesByIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0)
		{
			return [];
		}

		var query = await cycleRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, Employee>> GetEmployeesByIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0)
		{
			return [];
		}

		var query = await employeeRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, PerformanceTemplate>> GetTemplatesByIdsAsync(List<Guid> ids)
	{
		if (ids.Count == 0)
		{
			return [];
		}

		var query = await templateRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}
}
