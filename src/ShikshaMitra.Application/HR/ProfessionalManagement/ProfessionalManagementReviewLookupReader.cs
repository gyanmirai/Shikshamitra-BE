using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalManagementReviewLookupReader(
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceTemplateSection, Guid> sectionRepository,
	IRepository<PerformanceTemplateCriterion, Guid> criterionRepository,
	IRepository<PerformanceReviewStage, Guid> stageRepository,
	IRepository<PerformanceReviewResponse, Guid> responseRepository,
	IRepository<PerformanceOutcome, Guid> outcomeRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<PerformanceTemplateDto> MapTemplateAsync(PerformanceTemplate template)
	{
		var sections = await GetSectionsAsync(template.Id);
		var criteria = await GetCriteriaAsync(template.Id);
		return ProfessionalManagementTemplateProjection.ToTemplateDto(template, sections, criteria);
	}

	public async Task<List<PerformanceTemplateDto>> MapTemplatesAsync(List<PerformanceTemplate> templates)
	{
		var result = new List<PerformanceTemplateDto>();
		foreach (var template in templates)
		{
			result.Add(await MapTemplateAsync(template));
		}

		return result;
	}

	public async Task<List<PerformanceReviewStageDto>> MapStagesAsync(Guid reviewId)
	{
		var stageQuery = await stageRepository.GetQueryableAsync();
		var stages = await asyncExecuter.ToListAsync(stageQuery
			.Where(x => x.ReviewAssignmentId == reviewId)
			.OrderBy(x => x.StageType));
		var responseQuery = await responseRepository.GetQueryableAsync();
		var responses = await asyncExecuter.ToListAsync(responseQuery.Where(x => x.ReviewAssignmentId == reviewId));
		return stages.Select(s => ProfessionalManagementProjection.ToStageDto(s, responses)).ToList();
	}

	public async Task<PerformanceOutcomeDto?> MapOutcomeForReviewAsync(Guid reviewId)
	{
		var query = await outcomeRepository.GetQueryableAsync();
		var outcome = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.ReviewAssignmentId == reviewId));
		return outcome == null ? null : ProfessionalManagementProjection.ToOutcomeDto(outcome);
	}

	public async Task<Dictionary<Guid, ProfessionalReviewCycle>> GetCyclesByIdsAsync(List<Guid> ids)
	{
		var query = await cycleRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	public async Task<Dictionary<Guid, Employee>> GetEmployeesByIdsAsync(List<Guid> ids)
	{
		var query = await employeeRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	public async Task<Dictionary<Guid, PerformanceTemplate>> GetTemplatesByIdsAsync(List<Guid> ids)
	{
		var query = await templateRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
		return rows.ToDictionary(x => x.Id);
	}

	private async Task<List<PerformanceTemplateSection>> GetSectionsAsync(Guid templateId)
	{
		var query = await sectionRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.TemplateId == templateId).OrderBy(x => x.DisplayOrder));
	}

	private async Task<List<PerformanceTemplateCriterion>> GetCriteriaAsync(Guid templateId)
	{
		var query = await criterionRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.TemplateId == templateId).OrderBy(x => x.DisplayOrder));
	}
}
