using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
public class HrReportTemplateService(
	IRepository<HrReportTemplate, Guid> templateRepository,
	IGuidGenerator guidGenerator) : ApplicationService, ITransientDependency
{
	public async Task<HrReportTemplateListDto> GetTemplatesAsync()
	{
		var userId = GetRequiredUserId();
		var query = await templateRepository.GetQueryableAsync();

		var templates = await AsyncExecuter.ToListAsync(
			query
				.Where(x =>
					x.OwnerUserId == userId ||
					(x.Visibility == HrReportTemplateVisibility.Organization && x.Status == HrReportTemplateStatus.Published))
				.OrderBy(x => x.Visibility)
				.ThenBy(x => x.Name));

		return new HrReportTemplateListDto
		{
			Items = templates.Select(HrReportProjection.ToTemplateDto).ToList()
		};
	}

	public async Task<HrReportTemplateDto> CreateTemplateAsync(HrReportTemplateRequestDto input)
	{
		var userId = GetRequiredUserId();
		ValidateTemplateDefinition(input);
		var template = new HrReportTemplate(
			guidGenerator.Create(),
			CurrentTenant.Id,
			userId,
			input.Name,
			input.Description,
			input.Category,
			input.DataSource,
			input.Visibility,
			input.Status,
			JsonSerializer.Serialize(input.Definition));

		await templateRepository.InsertAsync(template, autoSave: true);
		return HrReportProjection.ToTemplateDto(template);
	}

	public async Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, HrReportTemplateRequestDto input)
	{
		ValidateTemplateDefinition(input);
		var template = await GetEditableTemplateAsync(id);
		template.Update(
			input.Name,
			input.Description,
			input.Category,
			input.DataSource,
			input.Visibility,
			input.Status,
			JsonSerializer.Serialize(input.Definition));

		await templateRepository.UpdateAsync(template, autoSave: true);
		return HrReportProjection.ToTemplateDto(template);
	}

	public async Task<HrReportTemplateDto> DuplicateTemplateAsync(Guid id)
	{
		var userId = GetRequiredUserId();
		var source = await GetReadableTemplateAsync(id);
		var clone = source.Clone(guidGenerator.Create(), userId);
		await templateRepository.InsertAsync(clone, autoSave: true);
		return HrReportProjection.ToTemplateDto(clone);
	}

	public async Task DeleteTemplateAsync(Guid id)
	{
		var template = await GetEditableTemplateAsync(id);
		await templateRepository.DeleteAsync(template, autoSave: true);
	}

	private async Task<HrReportTemplate> GetReadableTemplateAsync(Guid id)
	{
		var userId = GetRequiredUserId();
		var template = await templateRepository.GetAsync(id);
		if (template.OwnerUserId != userId &&
			!(template.Visibility == HrReportTemplateVisibility.Organization && template.Status == HrReportTemplateStatus.Published))
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied);
		}

		return template;
	}

	private async Task<HrReportTemplate> GetEditableTemplateAsync(Guid id)
	{
		var userId = GetRequiredUserId();
		var template = await templateRepository.GetAsync(id);
		if (template.OwnerUserId != userId)
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied);
		}

		return template;
	}

	private Guid GetRequiredUserId() =>
		CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);

	private static void ValidateTemplateDefinition(HrReportTemplateRequestDto input)
	{
		input.Definition.DataSource = string.IsNullOrWhiteSpace(input.Definition.DataSource)
			? input.DataSource
			: input.Definition.DataSource;
		input.Definition.ReportType = string.IsNullOrWhiteSpace(input.Definition.ReportType)
			? input.Name
			: input.Definition.ReportType;
	}
}
