using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentImpactService(
	IEarningComponentRepository repository,
	EarningComponentManager earningComponentManager,
	PayrollComponentImpactPreviewReader impactPreviewReader) : ITransientDependency
{
	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateEarningComponentDto input,
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await repository.GetAsync(id);
		PayrollComponentMutationRules.EnsureActive(earningComponent.IsActive);
		await earningComponentManager.ValidateDependencyAsync(
			id,
			input.CalculationType,
			input.SelectedEarningComponentId,
			input.DependOn);
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
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		await repository.GetAsync(id);
		await earningComponentManager.EnsureCanRetireAsync(id);
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
}
