using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementSqaaDashboardReader(
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceTemplateCriterion, Guid> criterionRepository,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceEvidence, Guid> evidenceRepository,
	IAsyncQueryableExecuter asyncExecuter,
    ProfessionalManagementReadScopeService readScope,
	ProfessionalManagementSqaaAuditPackBuilder auditPackBuilder) : ITransientDependency
{
	public async Task<List<PerformanceSqaaEvidenceDto>> GetSqaaEvidenceAsync(
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

		var criterionQuery = await criterionRepository.GetQueryableAsync();
		var criteria = await asyncExecuter.ToListAsync(criterionQuery
			.Where(x => templateIds.Contains(x.TemplateId))
			.WhereIf(sqaaDomain.HasValue, x => x.SqaaDomain == sqaaDomain));
		var criterionIds = criteria.Select(x => x.Id).ToHashSet();

		var assignmentQuery = await readScope.ApplyAsync(await assignmentRepository.GetQueryableAsync());
		var assignments = await asyncExecuter.ToListAsync(assignmentQuery
			.Where(x => templateIds.Contains(x.TemplateId))
			.WhereIf(cycleId.HasValue, x => x.CycleId == cycleId));
		var assignmentIds = assignments.Select(x => x.Id).ToHashSet();

		var evidenceQuery = await evidenceRepository.GetQueryableAsync();
		var evidence = await asyncExecuter.ToListAsync(evidenceQuery.Where(x =>
			assignmentIds.Contains(x.ReviewAssignmentId) &&
			x.CriterionId.HasValue &&
			criterionIds.Contains(x.CriterionId.Value)));

		return criteria
			.GroupBy(x => x.SqaaDomain)
			.Select(group =>
			{
				var groupCriterionIds = group.Select(x => x.Id).ToHashSet();
				var groupTemplateIds = group.Select(x => x.TemplateId).Distinct().ToHashSet();
				var criteriaByTemplate = group
					.Where(x => x.EvidenceRequired)
					.GroupBy(x => x.TemplateId)
					.ToDictionary(x => x.Key, x => x.Select(c => c.Id).ToHashSet());
				var domainAssignments = assignments.Where(x => groupTemplateIds.Contains(x.TemplateId)).ToList();
				var submittedEvidencePairs = evidence
					.Where(x => x.CriterionId.HasValue && groupCriterionIds.Contains(x.CriterionId.Value))
					.Select(x => (x.ReviewAssignmentId, CriterionId: x.CriterionId!.Value))
					.ToHashSet();
				var missingEvidence = domainAssignments.Sum(assignment =>
					(criteriaByTemplate.GetValueOrDefault(assignment.TemplateId) ?? [])
						.Count(criterionId => !submittedEvidencePairs.Contains((assignment.Id, criterionId))));

				return new PerformanceSqaaEvidenceDto
				{
					Domain = group.Key,
					TemplateCount = groupTemplateIds.Count,
					AssignedReviewCount = domainAssignments.Count,
					MappedCriteria = group.Count(),
					EvidenceRequiredCriteria = group.Count(x => x.EvidenceRequired),
					EvidenceSubmitted = evidence.Count(x => x.CriterionId.HasValue && groupCriterionIds.Contains(x.CriterionId.Value)),
					MissingEvidence = missingEvidence
				};
			})
			.OrderBy(x => x.Domain)
			.ToList();
	}

	public async Task<List<PerformanceSqaaReadinessDomainDto>> GetSqaafReadinessAsync(
		Guid? cycleId,
		PerformanceBoardType? boardType,
		PerformanceStaffCategory? staffCategory,
		PerformanceSqaaDomain? sqaaDomain)
	{
		var auditPack = await auditPackBuilder.BuildAsync(cycleId, boardType, staffCategory, sqaaDomain);
		return auditPack.Domains;
	}

	public Task<PerformanceSqaaAuditPackDto> GetSqaafAuditPackAsync(
		Guid? cycleId,
		PerformanceBoardType? boardType,
		PerformanceStaffCategory? staffCategory,
		PerformanceSqaaDomain? sqaaDomain) =>
		auditPackBuilder.BuildAsync(cycleId, boardType, staffCategory, sqaaDomain);

	public async Task<PerformanceSqaaAuditPackExportDto> GetSqaafAuditPackExportAsync(
		Guid? cycleId,
		PerformanceBoardType? boardType,
		PerformanceStaffCategory? staffCategory,
		PerformanceSqaaDomain? sqaaDomain)
	{
		var auditPack = await GetSqaafAuditPackAsync(cycleId, boardType, staffCategory, sqaaDomain);
		var csv = ProfessionalManagementSqaaAuditPackCsvBuilder.Build(auditPack);
		return new PerformanceSqaaAuditPackExportDto
		{
			FileName = $"sqaaf-audit-pack-{auditPack.GeneratedAt:yyyyMMddHHmmss}.csv",
			ContentType = "text/csv",
			ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)),
			GeneratedAt = auditPack.GeneratedAt
		};
	}
}
