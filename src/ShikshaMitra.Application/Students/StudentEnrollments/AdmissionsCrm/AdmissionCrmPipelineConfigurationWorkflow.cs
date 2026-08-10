using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPipelineConfigurationWorkflow(
	IRepository<AdmissionCrmPipelineStage, Guid> repository,
	AdmissionCrmConfigurationAccess access,
	AdmissionCrmConfigurationImpactWorkflow impact) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetAsync(Guid yearId)
	{
		await EnsureCanReadAsync();
		var configuration = await access.EnsureCycleAsync(yearId);
		var stages = await access.EnsurePipelineStagesAsync(yearId);
		var ordered = stages.OrderBy(x => x.DisplayOrder).ThenBy(x => x.SystemStage)
			.Select(x => AdmissionCrmConfigurationMapper.PipelineStage(x, configuration.JourneyLocked)).ToList();
		return new PagedResultDto<AdmissionCrmPipelineStageDto>(ordered.Count, ordered);
	}

	public async Task<AdmissionCrmPipelineStageDto> CreateAsync(Guid yearId, CreateAdmissionCrmPipelineStageDto input)
	{
		await access.EnsureYearMutationAsync(yearId, "CreatePipelineStageAsync");
		var configuration = await access.EnsureCycleAsync(yearId);
		AdmissionCrmCycleConfigurationWorkflow.EnsureJourneyEditable(configuration);
		await EnsureUniqueKeyAsync(yearId, input.StageKey, null);
		var stage = AdmissionCrmPipelineStage.CreateCustom(GuidGenerator.Create(), CurrentTenant.Id, yearId,
			input.StageKey, input.BaseSystemStage, input.StageName, input.DisplayOrder, input.ColorHex, input.SlaTargetHours,
			input.OwnerResponsibility, AdmissionCrmConfigurationMapper.SerializeAllowedNextStages(input.AllowedNextStages ?? []));
		await repository.InsertAsync(stage, autoSave: true);
		return AdmissionCrmConfigurationMapper.PipelineStage(stage, configuration.JourneyLocked);
	}

	public async Task<AdmissionCrmPipelineStageDto> UpdateAsync(Guid id, UpdateAdmissionCrmPipelineStageDto input)
	{
		var stage = await access.GetStageAsync(id, "UpdatePipelineStageAsync");
		await access.EnsureYearMutationAsync(stage.AcademicYearId, "UpdatePipelineStageAsync");
		await impact.EnsureFingerprintAsync(stage.AcademicYearId, new PreviewAdmissionCrmConfigurationImpactDto
			{ PipelineStageId = stage.Id, ProposedPipelineStage = input }, input.ImpactPreviewFingerprint, "UpdatePipelineStageAsync");
		var configuration = await access.EnsureCycleAsync(stage.AcademicYearId);
		if (configuration.JourneyLocked && (input.StageName != stage.StageName || input.DisplayOrder != stage.DisplayOrder ||
			input.IsEnabled != stage.IsEnabled || input.AllowedNextStages != null)) throw new BusinessException("AdmissionCrm:JourneyLocked");
		stage.Update(input.StageName, input.DisplayOrder, input.IsEnabled, input.ColorHex ?? stage.ColorHex,
			input.SlaTargetHours ?? stage.SlaTargetHours, input.OwnerResponsibility ?? stage.OwnerResponsibility,
			input.AllowedNextStages == null ? stage.AllowedNextStagesJson : AdmissionCrmConfigurationMapper.SerializeAllowedNextStages(input.AllowedNextStages));
		await repository.UpdateAsync(stage, autoSave: true);
		return AdmissionCrmConfigurationMapper.PipelineStage(stage, configuration.JourneyLocked);
	}

	private async Task EnsureCanReadAsync()
	{
		var canView = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
		var canAdd = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add);
		var canManage = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration);
		if (!canView && !canAdd && !canManage) throw new AbpAuthorizationException(
			"Admission CRM View, Add, or Manage Configuration permission is required to read pipeline stages.");
	}

	private async Task EnsureUniqueKeyAsync(Guid yearId, string stageKey, Guid? excludeId)
	{
		var normalized = stageKey.Trim().ToUpperInvariant();
		var queryable = await repository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId &&
			x.StageKey == normalized && (!excludeId.HasValue || x.Id != excludeId.Value))))
			throw new BusinessException("AdmissionCrm:DuplicatePipelineStageKey").WithData("StageKey", normalized);
	}
}
