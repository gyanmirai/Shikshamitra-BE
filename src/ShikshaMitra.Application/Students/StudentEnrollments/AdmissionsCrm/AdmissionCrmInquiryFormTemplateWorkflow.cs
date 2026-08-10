using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmInquiryFormTemplateWorkflow(
	IRepository<AdmissionInquiryFormTemplate, Guid> repository,
	AdmissionCrmConfigurationAccess access,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmFormTemplateDto> CreateAsync(Guid yearId, CreateAdmissionCrmFormTemplateDto input)
	{
		await access.EnsureYearMutationAsync(yearId, "CreateFormTemplateAsync");
		await EnsureUniqueNameAsync(yearId, input.TemplateName, null);
		var template = new AdmissionInquiryFormTemplate(GuidGenerator.Create(), CurrentTenant.Id, yearId,
			input.TemplateName, input.Description, input.Channel, input.IsPublic, input.ConsentText,
			input.PrivacyNoticeUrl, AdmissionCrmConfigurationMapper.SerializeFields(input.Fields),
			JsonSerializer.Serialize(input.Localizations), input.IsActive);
		await repository.InsertAsync(template, autoSave: true);
		return AdmissionCrmConfigurationMapper.FormTemplate(template);
	}

	public async Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetListAsync(
		Guid yearId, GetAdmissionCrmFormTemplateListInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "GetFormTemplatesAsync");
		var query = (await repository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			query = query.Where(x => x.TemplateName.ToLower().Contains(filter) ||
				(x.Description != null && x.Description.ToLower().Contains(filter)));
		}
		if (input.IsActive.HasValue) query = query.Where(x => x.IsActive == input.IsActive.Value);
		if (input.IsPublic.HasValue) query = query.Where(x => x.IsPublic == input.IsPublic.Value);
		if (input.Channel.HasValue) query = query.Where(x => x.Channel == input.Channel.Value);
		var total = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.IsActive)
			.ThenBy(x => x.TemplateName).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmFormTemplateDto>(total,
			items.Select(AdmissionCrmConfigurationMapper.FormTemplate).ToList());
	}

	public async Task<AdmissionCrmFormTemplateDto> GetAsync(Guid id) =>
		AdmissionCrmConfigurationMapper.FormTemplate(await access.GetFormAsync(id, "GetFormTemplateAsync"));

	public async Task<AdmissionCrmFormTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmFormTemplateDto input)
	{
		var template = await access.GetFormAsync(id, "UpdateFormTemplateAsync");
		await access.EnsureYearMutationAsync(template.AcademicYearId, "UpdateFormTemplateAsync");
		await EnsureUniqueNameAsync(template.AcademicYearId, input.TemplateName, template.Id);
		template.Update(input.TemplateName, input.Description, input.Channel, input.IsPublic, input.ConsentText,
			input.PrivacyNoticeUrl, AdmissionCrmConfigurationMapper.SerializeFields(input.Fields),
			JsonSerializer.Serialize(input.Localizations));
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmConfigurationMapper.FormTemplate(template);
	}

	public async Task<AdmissionCrmFormTemplateDto> UpdateStatusAsync(Guid id, UpdateAdmissionCrmFormTemplateStatusDto input)
	{
		var template = await access.GetFormAsync(id, "UpdateFormTemplateStatusAsync");
		await access.EnsureYearMutationAsync(template.AcademicYearId, "UpdateFormTemplateStatusAsync");
		if (input.IsActive) template.Activate(); else template.Deactivate();
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmConfigurationMapper.FormTemplate(template);
	}

	private async Task EnsureUniqueNameAsync(Guid yearId, string name, Guid? excludeId)
	{
		var normalized = name.Trim().ToLowerInvariant();
		var queryable = await repository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == yearId && x.TemplateName.ToLower() == normalized &&
			(!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException("AdmissionCrm:DuplicateFormTemplateName").WithData("TemplateName", name);
	}
}
