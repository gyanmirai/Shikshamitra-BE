using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationProviderWorkflow(
	IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> providerRepository,
	StudentScopeAccessService scopeAccess)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmCommunicationProviderDto>> GetAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, "GetProvidersAsync");
		var providers = new List<AdmissionCrmCommunicationProviderConfiguration>();
		foreach (var channel in Enum.GetValues<AdmissionCrmMessageChannel>())
			providers.Add(await GetOrCreateAsync(academicYearId, channel));
		var ordered = providers.OrderBy(x => x.Channel).ToList();
		return new PagedResultDto<AdmissionCrmCommunicationProviderDto>(ordered.Count,
			ordered.Select(AdmissionCrmCommunicationMapper.Provider).ToList());
	}

	public async Task<AdmissionCrmCommunicationProviderDto> UpdateAsync(
		Guid academicYearId,
		AdmissionCrmMessageChannel channel,
		UpdateAdmissionCrmCommunicationProviderDto input)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, "UpdateProviderAsync");
		var provider = await GetOrCreateAsync(academicYearId, channel);
		var settings = AdmissionCrmCommunicationProviderSettingsMapper.Merge(
			AdmissionCrmCommunicationProviderSettingsMapper.Deserialize(provider.SettingsJson), input.Settings);
		AdmissionCrmCommunicationProviderSettingsMapper.ValidateEndpoint(settings, input.WebhookUrl);
		provider.Update(input.IsEnabled, input.ProviderName, input.SenderId,
			AdmissionCrmCommunicationProviderSettingsMapper.Serialize(settings), input.WebhookUrl, input.LastValidatedAt);
		await providerRepository.UpdateAsync(provider, autoSave: true);
		return AdmissionCrmCommunicationMapper.Provider(provider);
	}

	public async Task<AdmissionCrmCommunicationProviderConfiguration?> FindAsync(Guid academicYearId, AdmissionCrmMessageChannel channel)
	{
		var queryable = await providerRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.Channel == channel));
	}

	private async Task<AdmissionCrmCommunicationProviderConfiguration> GetOrCreateAsync(Guid academicYearId, AdmissionCrmMessageChannel channel)
	{
		var existing = await FindAsync(academicYearId, channel);
		if (existing is not null) return existing;
		var provider = new AdmissionCrmCommunicationProviderConfiguration(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, channel);
		await providerRepository.InsertAsync(provider, autoSave: true);
		return provider;
	}
}
