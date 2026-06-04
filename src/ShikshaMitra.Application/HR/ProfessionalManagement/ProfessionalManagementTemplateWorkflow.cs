using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementTemplateWorkflow(
	IRepository<PerformanceTemplate, Guid> templateRepository,
	IRepository<PerformanceTemplateSection, Guid> sectionRepository,
	IRepository<PerformanceTemplateCriterion, Guid> criterionRepository,
	ProfessionalManagementReviewReader reviewReader,
	PerformanceAuditEventManager auditEventManager,
	PerformanceTemplateManager templateManager,
	IGuidGenerator guidGenerator,
	IAsyncQueryableExecuter asyncExecuter) : ApplicationService, ITransientDependency
{
	private const int MaxPageSize = 100;

	public async Task<PerformanceTemplateListResponse> GetTemplatesAsync(
		int page,
		int pageSize,
		string? search,
		PerformanceStaffCategory? staffCategory)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
		var query = await templateRepository.GetQueryableAsync();
		query = query
			.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!))
			.WhereIf(staffCategory.HasValue, x => x.StaffCategory == staffCategory);
		var totalCount = await asyncExecuter.CountAsync(query);
		var templates = await asyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.StaffCategory)
				.ThenBy(x => x.Name)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));
		return new PerformanceTemplateListResponse
		{
			TotalCount = totalCount,
			Items = await reviewReader.MapTemplatesAsync(templates)
		};
	}

	public async Task<PerformanceTemplateDto> CreateTemplateAsync(PerformanceTemplateRequest input)
	{
		var template = await CreateTemplateInternalAsync(input);
		await AuditAsync(null, "TemplateCreated", $"Created performance template {template.Name}");
		return await reviewReader.MapTemplateAsync(template);
	}

	public async Task<PerformanceTemplateDto> UpdateTemplateAsync(Guid id, PerformanceTemplateRequest input)
	{
		var template = await templateRepository.GetAsync(id);
		template.Update(input.Name, input.StaffCategory, input.RoleName, input.RatingScaleMax, input.AllowNotApplicable, input.Description);
		await templateRepository.UpdateAsync(template, autoSave: true);
		await ReplaceTemplateChildrenAsync(template.Id, input);
		await AuditAsync(null, "TemplateUpdated", $"Updated performance template {template.Name}");
		return await reviewReader.MapTemplateAsync(template);
	}

	public async Task<PerformanceTemplateDto> PublishTemplateAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		var sections = await GetSectionsAsync(template.Id);
		var criteria = await GetCriteriaAsync(template.Id);
		templateManager.Publish(template, sections, criteria);
		await templateRepository.UpdateAsync(template, autoSave: true);
		await AuditAsync(null, "TemplatePublished", $"Published performance template {template.Name}");
		return await reviewReader.MapTemplateAsync(template);
	}

	public async Task<PerformanceTemplateDto> CloneTemplateAsync(Guid id)
	{
		var source = await reviewReader.MapTemplateAsync(await templateRepository.GetAsync(id));
		var request = new PerformanceTemplateRequest
		{
			Name = $"{source.Name} Copy",
			StaffCategory = source.StaffCategory,
			RoleName = source.RoleName,
			RatingScaleMax = source.RatingScaleMax,
			AllowNotApplicable = source.AllowNotApplicable,
			Description = source.Description,
			Sections = source.Sections.Select(s => new PerformanceTemplateSectionRequest
			{
				Name = s.Name,
				Weight = s.Weight,
				DisplayOrder = s.DisplayOrder,
				Criteria = s.Criteria.Select(c => new PerformanceTemplateCriterionRequest
				{
					Name = c.Name,
					Description = c.Description,
					Weight = c.Weight,
					EvidenceRequired = c.EvidenceRequired,
					DisplayOrder = c.DisplayOrder
				}).ToList()
			}).ToList()
		};
		return await CreateTemplateAsync(request);
	}

	public async Task<PerformanceTemplateDto> ArchiveTemplateAsync(Guid id)
	{
		var template = await templateRepository.GetAsync(id);
		template.Archive();
		await templateRepository.UpdateAsync(template, autoSave: true);
		await AuditAsync(null, "TemplateArchived", $"Archived performance template {template.Name}");
		return await reviewReader.MapTemplateAsync(template);
	}

	public DefaultPerformanceTemplatesResponse GetDefaultTemplates() =>
		new()
		{
			Items = DefaultPerformanceTemplateCatalog.GetDefaultTemplateRequests()
		};

	public async Task<PerformanceTemplate> CreateTemplateInternalAsync(PerformanceTemplateRequest input)
	{
		var template = new PerformanceTemplate(
			guidGenerator.Create(),
			CurrentTenant.Id,
			input.Name,
			input.StaffCategory,
			input.RoleName,
			input.Description);
		template.Update(input.Name, input.StaffCategory, input.RoleName, input.RatingScaleMax, input.AllowNotApplicable, input.Description);
		await templateRepository.InsertAsync(template, autoSave: true);
		await ReplaceTemplateChildrenAsync(template.Id, input);
		return template;
	}

	public async Task<List<PerformanceTemplateSection>> GetSectionsAsync(Guid templateId)
	{
		var query = await sectionRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.TemplateId == templateId).OrderBy(x => x.DisplayOrder));
	}

	public async Task<List<PerformanceTemplateCriterion>> GetCriteriaAsync(Guid templateId)
	{
		var query = await criterionRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.TemplateId == templateId).OrderBy(x => x.DisplayOrder));
	}

	private async Task ReplaceTemplateChildrenAsync(Guid templateId, PerformanceTemplateRequest input)
	{
		foreach (var existingCriterion in await GetCriteriaAsync(templateId))
		{
			await criterionRepository.DeleteAsync(existingCriterion, autoSave: true);
		}

		foreach (var existingSection in await GetSectionsAsync(templateId))
		{
			await sectionRepository.DeleteAsync(existingSection, autoSave: true);
		}

		foreach (var sectionInput in input.Sections)
		{
			var section = new PerformanceTemplateSection(
				guidGenerator.Create(),
				CurrentTenant.Id,
				templateId,
				sectionInput.Name,
				sectionInput.Weight,
				sectionInput.DisplayOrder);
			await sectionRepository.InsertAsync(section, autoSave: true);

			foreach (var criterionInput in sectionInput.Criteria)
			{
				var criterion = new PerformanceTemplateCriterion(
					guidGenerator.Create(),
					CurrentTenant.Id,
					templateId,
					section.Id,
					criterionInput.Name,
					criterionInput.Description,
					criterionInput.Weight,
					criterionInput.EvidenceRequired,
					criterionInput.DisplayOrder);
				await criterionRepository.InsertAsync(criterion, autoSave: true);
			}
		}
	}

	private async Task AuditAsync(Guid? reviewId, string eventType, string summary) =>
		await auditEventManager.RecordAsync(reviewId, eventType, summary);
}
