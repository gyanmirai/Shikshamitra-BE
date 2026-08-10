using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlatformPresetWorkflow(
	IRepository<AdmissionCrmPlatformPreset, Guid> presetRepository,
	IRepository<AdmissionCrmPresetAdoption, Guid> adoptionRepository,
	AdmissionCrmSupportSessionWorkflow supportSessions,
	AdmissionCrmCycleConfigurationWorkflow configurationWorkflow,
	AdmissionCrmConfigurationImpactWorkflow configurationImpact,
	AdmissionCrmSettingsAccessWorkflow settingsAccess) : ApplicationService, ITransientDependency
{
	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public async Task<AdmissionCrmPlatformPresetDto> CreatePresetAsync(CreateAdmissionCrmPlatformPresetDto input)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		var configurationJson = JsonSerializer.Serialize(input.Configuration, AdmissionCrmSetupFormat.JsonOptions);
		var accessJson = JsonSerializer.Serialize(input.AccessPolicy, AdmissionCrmSetupFormat.JsonOptions);
		var preset = new AdmissionCrmPlatformPreset(GuidGenerator.Create(), input.Code, input.Name, input.Version, configurationJson, accessJson, input.ReleaseNotes);
		await presetRepository.InsertAsync(preset, autoSave: true);
		return AdmissionCrmSetupFormat.ToDto(preset);
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public async Task<AdmissionCrmPlatformPresetDto> PublishPresetAsync(Guid id)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		var preset = await presetRepository.GetAsync(id);
		preset.Publish(Clock.Now);
		await presetRepository.UpdateAsync(preset, autoSave: true);
		return AdmissionCrmSetupFormat.ToDto(preset);
	}

	[Authorize(AdmissionCrmPlatformPermissions.ManagePresets)]
	public async Task<PagedResultDto<AdmissionCrmPlatformPresetDto>> GetPresetsAsync(PagedResultRequestDto input)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		var query = await presetRepository.GetQueryableAsync();
		var total = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Code).ThenByDescending(x => x.Version).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmPlatformPresetDto>(total, items.Select(AdmissionCrmSetupFormat.ToDto).ToList());
	}

	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public async Task<AdmissionCrmPresetPreviewDto> PreviewPresetAsync(AdmissionCrmPresetOperationDto input)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		await supportSessions.GetOwnedSessionAsync(input.SupportSessionId, input.TenantId);
		var preset = await GetPublishedPresetAsync(input.PresetId);
		AdmissionCrmCycleConfigurationDto current;
		using (CurrentTenant.Change(input.TenantId)) current = await configurationWorkflow.GetAsync(input.AcademicYearId);
		var currentJson = JsonSerializer.Serialize(current, AdmissionCrmSetupFormat.JsonOptions);
		var currentFields = JsonDocument.Parse(currentJson).RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var presetFields = JsonDocument.Parse(preset.ConfigurationJson).RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var changed = presetFields.Where(x => !currentFields.TryGetValue(x.Key, out var value) || value != x.Value).Select(x => x.Key).ToList();
		return new AdmissionCrmPresetPreviewDto { PresetId = preset.Id, PresetVersion = preset.Version, HasChanges = changed.Count > 0, ChangedFields = changed };
	}

	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public async Task<AdmissionCrmPresetApplyResultDto> ApplyPresetAsync(AdmissionCrmPresetOperationDto input)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		await supportSessions.GetOwnedSessionAsync(input.SupportSessionId, input.TenantId);
		var preset = await GetPublishedPresetAsync(input.PresetId);
		AdmissionCrmCycleConfigurationDto current;
		using (CurrentTenant.Change(input.TenantId)) current = await configurationWorkflow.GetAsync(input.AcademicYearId);
		var previousJson = JsonSerializer.Serialize(current, AdmissionCrmSetupFormat.JsonOptions);
		var currentHash = AdmissionCrmSetupFormat.Hash(previousJson);
		if (!string.IsNullOrWhiteSpace(input.ExpectedSnapshotHash) && !string.Equals(input.ExpectedSnapshotHash, currentHash, StringComparison.Ordinal)) throw new BusinessException("AdmissionCrm:PresetApplyConcurrencyConflict");
		var configuration = JsonSerializer.Deserialize<UpdateAdmissionCrmCycleConfigurationDto>(preset.ConfigurationJson, AdmissionCrmSetupFormat.JsonOptions) ?? throw new BusinessException("AdmissionCrm:InvalidPlatformPresetConfiguration");
		using (CurrentTenant.Change(input.TenantId))
		{
			configuration.ImpactPreviewFingerprint =
				await configurationImpact.CreateCycleFingerprintAsync(
					input.AcademicYearId,
					configuration,
					nameof(ApplyPresetAsync));
			await configurationWorkflow.UpdateAsync(input.AcademicYearId, configuration);
		}
		var adoption = new AdmissionCrmPresetAdoption(GuidGenerator.Create(), input.TenantId, input.AcademicYearId, preset.Id, preset.Version, AdmissionCrmSetupFormat.Hash(preset.ConfigurationJson), previousJson, Clock.Now);
		using (CurrentTenant.Change(input.TenantId)) await adoptionRepository.InsertAsync(adoption, autoSave: true);
		await settingsAccess.UpdateAccessPolicyCoreAsync(input.TenantId, JsonSerializer.Deserialize<List<UpdateAdmissionCrmSettingsAccessDto>>(preset.AccessPolicyJson, AdmissionCrmSetupFormat.JsonOptions) ?? []);
		return new AdmissionCrmPresetApplyResultDto { AdoptionId = adoption.Id, SnapshotHash = adoption.SnapshotHash, AppliedAt = adoption.AppliedAt };
	}

	[Authorize(AdmissionCrmPlatformPermissions.ApplyPresets)]
	public async Task RollbackPresetAsync(Guid adoptionId, Guid supportSessionId)
	{
		AdmissionCrmSetupFormat.EnsureHostContext(CurrentTenant.Id);
		AdmissionCrmPresetAdoption adoption;
		using (DataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>()) adoption = await adoptionRepository.GetAsync(adoptionId);
		await supportSessions.GetOwnedSessionAsync(supportSessionId, adoption.TenantId!.Value);
		if (adoption.RolledBackAt is not null) throw new BusinessException("AdmissionCrm:PresetAlreadyRolledBack");
		var previous = JsonSerializer.Deserialize<UpdateAdmissionCrmCycleConfigurationDto>(adoption.PreviousConfigurationJson, AdmissionCrmSetupFormat.JsonOptions) ?? throw new BusinessException("AdmissionCrm:InvalidRollbackSnapshot");
		using (CurrentTenant.Change(adoption.TenantId))
		{
			previous.ImpactPreviewFingerprint =
				await configurationImpact.CreateCycleFingerprintAsync(
					adoption.AcademicYearId,
					previous,
					nameof(RollbackPresetAsync));
			await configurationWorkflow.UpdateAsync(adoption.AcademicYearId, previous);
			adoption.MarkRolledBack(Clock.Now);
			await adoptionRepository.UpdateAsync(adoption, autoSave: true);
		}
	}

	private async Task<AdmissionCrmPlatformPreset> GetPublishedPresetAsync(Guid id)
	{
		var preset = await presetRepository.GetAsync(id);
		if (preset.Status != AdmissionCrmPlatformPresetStatus.Published) throw new BusinessException("AdmissionCrm:PlatformPresetNotPublished");
		return preset;
	}
}