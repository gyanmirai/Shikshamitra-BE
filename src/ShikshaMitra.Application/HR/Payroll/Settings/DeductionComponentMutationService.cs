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
public class DeductionComponentMutationService(
	IRepository<DeductionComponent, Guid> repository,
	DeductionComponentManager deductionComponentManager,
	IRepository<PayrollComponentVersion, Guid> componentVersionRepository,
	PayrollComponentImpactPreviewReader impactPreviewReader,
	PayrollComponentEmployeeSyncService employeeSyncService,
	IDistributedCache<PagedResultDto<DeductionComponentDto>> cache) : ApplicationService, ITransientDependency
{
	private const string CachePrefix = "DeductionComponents";

	public async Task<DeductionComponentDto> CreateAsync(CreateDeductionComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var deductionComponent = await deductionComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder);

		try
		{
			await repository.InsertAsync(deductionComponent, autoSave: true);
		}
		catch (Exception ex) when (IsNameUniqueViolation(ex))
		{
			throw new BusinessException(HRErrorCodes.DeductionComponentNameAlreadyExists)
				.WithData("name", input.Name);
		}

		await ClearCacheAsync();
		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public async Task<DeductionComponentDto> UpdateAsync(Guid id, UpdateDeductionComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var deductionComponent = await repository.GetAsync(id);
		EnsureActive(deductionComponent.IsActive);
		EnsurePreviewHashProvided(input.PreviewHash);

		var currentPreview = await PreviewUpdateImpactAsync(id, input);
		if (!string.Equals(currentPreview.PreviewHash, input.PreviewHash, StringComparison.Ordinal))
		{
			throw new BusinessException(HRErrorCodes.PayrollComponentPreviewExpired)
				.WithData("message", "Payroll component impact preview is no longer current. Please preview again before saving.");
		}

		await deductionComponentManager.UpdateAsync(
			deductionComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder);

		try
		{
			await repository.UpdateAsync(deductionComponent, autoSave: true);
		}
		catch (Exception ex) when (IsNameUniqueViolation(ex))
		{
			throw new BusinessException(HRErrorCodes.DeductionComponentNameAlreadyExists)
				.WithData("name", input.Name);
		}

		var effectiveMonth = ResolveEffectiveMonth(input.EffectiveMonth, Clock.Now);
		await ClearCacheAsync();
		await componentVersionRepository.InsertAsync(
			PayrollComponentVersion.FromDeductionComponent(GuidGenerator.Create(), effectiveMonth, deductionComponent),
			autoSave: true);
		await employeeSyncService.SyncDeductionAsync(deductionComponent, input.ApplyMode, effectiveMonth, Clock.Now);

		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateDeductionComponentDto input,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20)
	{
		ArgumentNullException.ThrowIfNull(input);

		var deductionComponent = await repository.GetAsync(id);
		EnsureActive(deductionComponent.IsActive);
		return await impactPreviewReader.BuildUpdatePreviewAsync(
			id,
			isEarning: false,
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
			isEarning: false,
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

		var deductionComponent = await repository.GetAsync(id);
		deductionComponent.Deactivate();
		await repository.UpdateAsync(deductionComponent, autoSave: false);

		await ClearCacheAsync();
		await employeeSyncService.DeleteLinkedRowsAsync(id, isEarning: false);
	}

	public async Task ActivateAsync(Guid id)
	{
		var deductionComponent = await repository.GetAsync(id);
		deductionComponent.Activate();
		await repository.UpdateAsync(deductionComponent, autoSave: true);
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var deductionComponent = await repository.GetAsync(id);
		deductionComponent.Deactivate();
		await repository.UpdateAsync(deductionComponent, autoSave: true);
		await ClearCacheAsync();
	}

	private async Task ClearCacheAsync() =>
		await ClearComponentListCacheAsync(cache, CachePrefix, CurrentTenant?.Id, Logger, "deduction");

	private static bool IsNameUniqueViolation(Exception exception)
	{
		const string indexName = "IX_DeductionComponents_TenantId_Name";
		const string tableHint = "AppDeductionComponents";
		return PayrollComponentMutationRules.IsNameUniqueViolation(exception, indexName, tableHint);
	}

}
