using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using static ShikshaMitra.HR.Payroll.Settings.PayrollComponentMutationRules;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class EarningComponentMutationService(
	IRepository<EarningComponent, Guid> repository,
	EarningComponentManager earningComponentManager,
	IRepository<PayrollComponentVersion, Guid> componentVersionRepository,
	PayrollComponentImpactPreviewReader impactPreviewReader,
	PayrollComponentEmployeeSyncService employeeSyncService,
	IDistributedCache<PagedResultDto<EarningComponentDto>> cache) : ApplicationService, ITransientDependency
{
	private const string CachePrefix = "EarningComponents";
	private const string NameIndexName = "IX_EarningComponents_TenantId_Name";
	private const string TableHint = "AppEarningComponents";

	public async Task<EarningComponentDto> CreateAsync(CreateEarningComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await earningComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder);

		try
		{
			await repository.InsertAsync(earningComponent, autoSave: true);
		}
		catch (Exception ex) when (PayrollComponentMutationRules.IsNameUniqueViolation(ex, NameIndexName, TableHint))
		{
			throw new BusinessException(HRErrorCodes.EarningComponentNameAlreadyExists)
				.WithData("name", input.Name);
		}

		await ClearCacheAsync();
		await employeeSyncService.SyncEarningAsync(
			earningComponent,
			PayrollComponentApplyMode.SyncedOnly,
			effectiveMonth: null,
			Clock.Now);

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public async Task<EarningComponentDto> UpdateAsync(Guid id, UpdateEarningComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await repository.GetAsync(id);
		EnsureActive(earningComponent.IsActive);
		EnsurePreviewHashProvided(input.PreviewHash);

		var currentPreview = await PreviewUpdateImpactAsync(id, input);
		if (!string.Equals(currentPreview.PreviewHash, input.PreviewHash, StringComparison.Ordinal))
		{
			throw new BusinessException(HRErrorCodes.PayrollComponentPreviewExpired)
				.WithData("message", "Payroll component impact preview is no longer current. Please preview again before saving.");
		}

		await earningComponentManager.UpdateAsync(
			earningComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder);

		try
		{
			await repository.UpdateAsync(earningComponent, autoSave: true);
		}
		catch (Exception ex) when (PayrollComponentMutationRules.IsNameUniqueViolation(ex, NameIndexName, TableHint))
		{
			throw new BusinessException(HRErrorCodes.EarningComponentNameAlreadyExists)
				.WithData("name", input.Name);
		}

		var effectiveMonth = ResolveEffectiveMonth(input.EffectiveMonth, Clock.Now);
		await ClearCacheAsync();
		await componentVersionRepository.InsertAsync(
			PayrollComponentVersion.FromEarningComponent(GuidGenerator.Create(), effectiveMonth, earningComponent),
			autoSave: true);
		await employeeSyncService.SyncEarningAsync(earningComponent, input.ApplyMode, effectiveMonth, Clock.Now);

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateEarningComponentDto input,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await repository.GetAsync(id);
		EnsureActive(earningComponent.IsActive);
		return await impactPreviewReader.BuildUpdatePreviewAsync(
			id,
			isEarning: true,
			input.EffectiveMonth,
			(effectiveMonth, syncedCount, overrideCount, finalizedSlipCount, draftSlipCount) =>
				PayrollComponentPreviewHashBuilder.BuildPreviewHash(
					id,
					input,
					effectiveMonth,
					syncedCount,
					overrideCount,
					finalizedSlipCount,
					draftSlipCount),
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewDeleteImpactAsync(
		Guid id,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20)
	{
		await repository.GetAsync(id);
		return await impactPreviewReader.BuildDeletePreviewAsync(
			id,
			isEarning: true,
			(employeeIds, syncedCount, overrideCount, finalizedSlipCount, draftSlipCount) =>
				PayrollComponentPreviewHashBuilder.BuildDeletePreviewHash(
					id,
					employeeIds,
					syncedCount,
					overrideCount,
					finalizedSlipCount,
					draftSlipCount),
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	public async Task DeleteConfirmedAsync(Guid id, string previewHash)
	{
		EnsureDeletePreviewHashProvided(previewHash);

		var currentPreview = await PreviewDeleteImpactAsync(id);
		if (!string.Equals(currentPreview.PreviewHash, previewHash, StringComparison.Ordinal))
		{
			throw new BusinessException(HRErrorCodes.PayrollComponentPreviewExpired)
				.WithData("message", "Payroll component delete impact preview is no longer current. Please preview again before deleting.");
		}

		var earningComponent = await repository.GetAsync(id);
		earningComponent.Deactivate();
		await repository.UpdateAsync(earningComponent, autoSave: false);

		await ClearCacheAsync();
		await employeeSyncService.DeleteLinkedRowsAsync(id, isEarning: true);
	}

	public async Task ActivateAsync(Guid id)
	{
		var earningComponent = await repository.GetAsync(id);
		earningComponent.Activate();
		await repository.UpdateAsync(earningComponent, autoSave: true);
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var earningComponent = await repository.GetAsync(id);
		earningComponent.Deactivate();
		await repository.UpdateAsync(earningComponent, autoSave: true);
		await ClearCacheAsync();
	}

	private async Task ClearCacheAsync() =>
		await ClearComponentListCacheAsync(cache, CachePrefix, CurrentTenant?.Id, Logger, "earning");
}
