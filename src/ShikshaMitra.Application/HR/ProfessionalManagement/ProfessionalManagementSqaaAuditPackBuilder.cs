using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementSqaaAuditPackBuilder(
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceTemplateCriterion, Guid> criterionRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceEvidence, Guid> evidenceRepository,
	IAsyncQueryableExecuter asyncExecuter,
    ProfessionalManagementReadScopeService readScope,
	IClock clock) : ITransientDependency
{
	public async Task<PerformanceSqaaAuditPackDto> BuildAsync(
		Guid? cycleId,
		PerformanceBoardType? boardType,
		PerformanceStaffCategory? staffCategory,
		PerformanceSqaaDomain? sqaaDomain)
	{
		var templateQuery = await templateRepository.GetQueryableAsync();
		templateQuery = templateQuery
			.WhereIf(boardType.HasValue, x => x.BoardType == boardType || x.BoardType == PerformanceBoardType.GenericIndian)
			.WhereIf(staffCategory.HasValue, x => x.StaffCategory == staffCategory);
		var templates = await asyncExecuter.ToListAsync(templateQuery);
		var templateIds = templates.Select(x => x.Id).ToHashSet();
		var templatesById = templates.ToDictionary(x => x.Id);

		var criterionQuery = await criterionRepository.GetQueryableAsync();
		var criteria = await asyncExecuter.ToListAsync(criterionQuery
			.Where(x => templateIds.Contains(x.TemplateId))
			.WhereIf(sqaaDomain.HasValue, x => x.SqaaDomain == sqaaDomain));
		var criteriaByTemplate = criteria
			.GroupBy(x => x.TemplateId)
			.ToDictionary(x => x.Key, x => x.ToList());

		var assignmentQuery = await readScope.ApplyAsync(await assignmentRepository.GetQueryableAsync());
		var assignments = await asyncExecuter.ToListAsync(assignmentQuery
			.Where(x => templateIds.Contains(x.TemplateId))
			.WhereIf(cycleId.HasValue, x => x.CycleId == cycleId));
		var assignmentIds = assignments.Select(x => x.Id).ToHashSet();

		var requiredCriterionIds = criteria.Where(x => x.EvidenceRequired).Select(x => x.Id).ToHashSet();
		var evidenceQuery = await evidenceRepository.GetQueryableAsync();
		var evidence = await asyncExecuter.ToListAsync(evidenceQuery.Where(x =>
			assignmentIds.Contains(x.ReviewAssignmentId) &&
			x.CriterionId.HasValue &&
			requiredCriterionIds.Contains(x.CriterionId.Value)));
		var submittedEvidencePairs = evidence
			.Select(x => (x.ReviewAssignmentId, CriterionId: x.CriterionId!.Value))
			.ToHashSet();

		var cyclesById = await GetCyclesByIdAsync(assignments);
		var employeesById = await GetEmployeesByIdAsync(assignments);
		var domains = BuildDomains(criteria, criteriaByTemplate, assignments, evidence, sqaaDomain);
		var missingEvidenceItems = BuildMissingEvidenceItems(
			assignments,
			criteriaByTemplate,
			submittedEvidencePairs,
			cyclesById,
			employeesById,
			templatesById,
			sqaaDomain);

		if (missingEvidenceItems.Count > 50000) throw new Volo.Abp.UserFriendlyException("More than 50,000 missing evidence items match. Choose a review cycle or domain before loading this table.");
		var totalRequiredEvidence = domains.Sum(x => x.EvidenceRequiredCriteria);
		var totalEvidenceSubmitted = domains.Sum(x => x.EvidenceSubmitted);
		return new PerformanceSqaaAuditPackDto
		{
			GeneratedAt = clock.Now,
			OverallReadinessPercentage = ProfessionalManagementSqaaMetrics.CalculateReadinessPercentage(totalEvidenceSubmitted, totalRequiredEvidence),
			TotalMappedCriteria = domains.Sum(x => x.MappedCriteria),
			TotalRequiredEvidence = totalRequiredEvidence,
			TotalEvidenceSubmitted = totalEvidenceSubmitted,
			TotalMissingEvidence = domains.Sum(x => x.MissingEvidence),
			Domains = domains,
			MissingEvidenceItems = missingEvidenceItems
		};
	}

	private async Task<Dictionary<Guid, ProfessionalReviewCycle>> GetCyclesByIdAsync(List<PerformanceReviewAssignment> assignments)
	{
		var cycleIds = assignments.Select(x => x.CycleId).Distinct().ToHashSet();
		var cycleQuery = await cycleRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(cycleQuery.Where(x => cycleIds.Contains(x.Id))))
			.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, Employee>> GetEmployeesByIdAsync(List<PerformanceReviewAssignment> assignments)
	{
		var employeeIds = assignments.Select(x => x.EmployeeId).Distinct().ToHashSet();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id))))
			.ToDictionary(x => x.Id);
	}

	private static List<PerformanceSqaaReadinessDomainDto> BuildDomains(
		List<PerformanceTemplateCriterion> criteria,
		Dictionary<Guid, List<PerformanceTemplateCriterion>> criteriaByTemplate,
		List<PerformanceReviewAssignment> assignments,
		List<PerformanceEvidence> evidence,
		PerformanceSqaaDomain? sqaaDomain) =>
		ProfessionalManagementSqaaMetrics.GetDomains(sqaaDomain)
			.Select(domain =>
			{
				var domainCriteria = criteria.Where(x => x.SqaaDomain == domain).ToList();
				var domainCriterionIds = domainCriteria.Select(x => x.Id).ToHashSet();
				var domainTemplateIds = domainCriteria.Select(x => x.TemplateId).Distinct().ToHashSet();
				var domainAssignments = assignments.Where(x => domainTemplateIds.Contains(x.TemplateId)).ToList();
				var requiredEvidenceInstances = domainAssignments.Sum(assignment =>
					criteriaByTemplate.GetValueOrDefault(assignment.TemplateId)?
						.Count(x => x.SqaaDomain == domain && x.EvidenceRequired) ?? 0);
				var submittedEvidence = evidence
					.Where(x => x.CriterionId.HasValue && domainCriterionIds.Contains(x.CriterionId.Value))
					.Select(x => (x.ReviewAssignmentId, CriterionId: x.CriterionId!.Value))
					.Distinct()
					.Count();
				var missingEvidence = Math.Max(0, requiredEvidenceInstances - submittedEvidence);

				return new PerformanceSqaaReadinessDomainDto
				{
					Domain = domain,
					ReadinessPercentage = ProfessionalManagementSqaaMetrics.CalculateReadinessPercentage(submittedEvidence, requiredEvidenceInstances),
					TemplateCount = domainTemplateIds.Count,
					AssignedReviewCount = domainAssignments.Count,
					MappedCriteria = domainCriteria.Count,
					EvidenceRequiredCriteria = requiredEvidenceInstances,
					EvidenceSubmitted = submittedEvidence,
					MissingEvidence = missingEvidence,
					IsReady = requiredEvidenceInstances > 0 && missingEvidence == 0
				};
			})
			.OrderBy(x => x.Domain)
			.ToList();

	private static List<PerformanceSqaaMissingEvidenceDto> BuildMissingEvidenceItems(
		List<PerformanceReviewAssignment> assignments,
		Dictionary<Guid, List<PerformanceTemplateCriterion>> criteriaByTemplate,
		HashSet<(Guid ReviewAssignmentId, Guid CriterionId)> submittedEvidencePairs,
		Dictionary<Guid, ProfessionalReviewCycle> cyclesById,
		Dictionary<Guid, Employee> employeesById,
		Dictionary<Guid, PerformanceTemplate> templatesById,
		PerformanceSqaaDomain? sqaaDomain) =>
		assignments
			.SelectMany(assignment =>
				(criteriaByTemplate.GetValueOrDefault(assignment.TemplateId) ?? [])
					.Where(x => x.EvidenceRequired && (!sqaaDomain.HasValue || x.SqaaDomain == sqaaDomain))
					.Where(criterion => !submittedEvidencePairs.Contains((assignment.Id, criterion.Id)))
					.Select(criterion => BuildMissingEvidenceItem(
						assignment,
						criterion,
						cyclesById.GetValueOrDefault(assignment.CycleId),
						employeesById.GetValueOrDefault(assignment.EmployeeId),
						templatesById.GetValueOrDefault(assignment.TemplateId))))
			.OrderBy(x => x.Domain)
			.ThenBy(x => x.CycleName)
			.ThenBy(x => x.EmployeeName)
			.ThenBy(x => x.CriterionName)
			.ToList();

	private static PerformanceSqaaMissingEvidenceDto BuildMissingEvidenceItem(
		PerformanceReviewAssignment assignment,
		PerformanceTemplateCriterion criterion,
		ProfessionalReviewCycle? cycle,
		Employee? employee,
		PerformanceTemplate? template) =>
		new()
		{
			ReviewId = assignment.Id,
			CycleId = assignment.CycleId,
			CycleName = cycle?.Name ?? "Unknown cycle",
			EmployeeId = assignment.EmployeeId,
			EmployeeName = string.IsNullOrWhiteSpace(employee?.EmployeeName) ? "Unknown staff" : employee.EmployeeName,
			EmployeeIdentifier = employee?.EmployeeCode,
			TemplateId = assignment.TemplateId,
			TemplateName = template?.Name ?? "Unknown template",
			CriterionId = criterion.Id,
			CriterionName = criterion.Name,
			Domain = criterion.SqaaDomain
		};
}
