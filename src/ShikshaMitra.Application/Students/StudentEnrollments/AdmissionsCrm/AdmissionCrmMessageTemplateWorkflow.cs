using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageTemplateWorkflow(
	IRepository<AdmissionCrmMessageTemplate, Guid> repository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	AdmissionCrmConfigurationAccess access,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmMessageTemplateDto> CreateAsync(Guid yearId, CreateAdmissionCrmMessageTemplateDto input)
	{
		await access.EnsureYearMutationAsync(yearId, "CreateMessageTemplateAsync");
		await EnsureUniqueNameAsync(yearId, input.Channel, input.TemplateName, null);
		var template = new AdmissionCrmMessageTemplate(GuidGenerator.Create(), CurrentTenant.Id, yearId,
			input.TemplateName, input.Channel, input.Purpose, input.Subject, input.Body,
			AdmissionCrmConfigurationMapper.SerializeVariables(input.Variables));
		await repository.InsertAsync(template, autoSave: true);
		return AdmissionCrmConfigurationMapper.MessageTemplate(template);
	}

	public async Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetListAsync(
		Guid yearId, GetAdmissionCrmMessageTemplateListInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "GetMessageTemplatesAsync");
		var query = (await repository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			query = query.Where(x => x.TemplateName.ToLower().Contains(filter) || x.Purpose.ToLower().Contains(filter) ||
				x.Body.ToLower().Contains(filter) || (x.Subject != null && x.Subject.ToLower().Contains(filter)));
		}
		if (input.Channel.HasValue) query = query.Where(x => x.Channel == input.Channel.Value);
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
		if (input.IsActive.HasValue) query = query.Where(x => x.IsActive == input.IsActive.Value);
		var total = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.IsActive).ThenBy(x => x.Channel)
			.ThenBy(x => x.TemplateName).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmMessageTemplateDto>(total,
			items.Select(AdmissionCrmConfigurationMapper.MessageTemplate).ToList());
	}

	public async Task<AdmissionCrmMessageTemplateDto> GetAsync(Guid id) =>
		AdmissionCrmConfigurationMapper.MessageTemplate(await access.GetMessageAsync(id, "GetMessageTemplateAsync"));

	public async Task<AdmissionCrmMessageTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmMessageTemplateDto input)
	{
		var template = await access.GetMessageAsync(id, "UpdateMessageTemplateAsync");
		await access.EnsureYearMutationAsync(template.AcademicYearId, "UpdateMessageTemplateAsync");
		await EnsureUniqueNameAsync(template.AcademicYearId, input.Channel, input.TemplateName, template.Id);
		template.Update(input.TemplateName, input.Channel, input.Purpose, input.Subject, input.Body,
			AdmissionCrmConfigurationMapper.SerializeVariables(input.Variables));
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmConfigurationMapper.MessageTemplate(template);
	}

	public async Task<AdmissionCrmMessageTemplateDto> UpdateStatusAsync(Guid id, UpdateAdmissionCrmMessageTemplateStatusDto input)
	{
		var template = await access.GetMessageAsync(id, "UpdateMessageTemplateStatusAsync");
		await access.EnsureYearMutationAsync(template.AcademicYearId, "UpdateMessageTemplateStatusAsync");
		if (template.Status == AdmissionCrmMessageTemplateStatus.Approved &&
			(input.Status != AdmissionCrmMessageTemplateStatus.Approved || !input.IsActive))
			await EnsureNotUsedByAutomationAsync(template.AcademicYearId, template.Id);
		template.UpdateStatus(input.Status, input.IsActive);
		await repository.UpdateAsync(template, autoSave: true);
		return AdmissionCrmConfigurationMapper.MessageTemplate(template);
	}

	private async Task EnsureUniqueNameAsync(Guid yearId, AdmissionCrmMessageChannel channel, string name, Guid? excludeId)
	{
		var normalized = name.Trim().ToLowerInvariant();
		var queryable = await repository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == yearId && x.Channel == channel && x.TemplateName.ToLower() == normalized &&
			(!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException("AdmissionCrm:DuplicateMessageTemplateName")
				.WithData("TemplateName", name).WithData("Channel", channel);
	}

	private async Task EnsureNotUsedByAutomationAsync(Guid academicYearId, Guid templateId)
	{
		var configurations = await AsyncExecuter.ToListAsync((await cycleRepository.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var journeyCount = configurations.SelectMany(x => AdmissionCrmJourneyRuleSerializer.DeserializeRules(x.JourneyRulesJson))
			.Count(x => x.IsEnabled && x.SendMessage && x.MessageTemplateId == templateId);
		var acknowledgementCount = configurations.SelectMany(x => AdmissionCrmChannelSettingsSerializer.DeserializeSettings(x.ChannelSettingsJson))
			.Count(x => x.IsEnabled && x.DefaultAcknowledgementTemplateId == templateId);
		if (journeyCount == 0 && acknowledgementCount == 0) return;
		throw new BusinessException("AdmissionCrm:MessageTemplateInUse").WithData("TemplateId", templateId)
			.WithData("JourneyRuleCount", journeyCount).WithData("ChannelAcknowledgementCount", acknowledgementCount);
	}
}
