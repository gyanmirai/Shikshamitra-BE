using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Setup;

[Authorize, RemoteService(false)]
public class FinanceSetupAppService(
	FinanceSetupAccess access,
	FinanceSetupConfigurationReader configuration, IRepository<AcademicYear, Guid> years) : ApplicationService, IFinanceSetupAppService
{

	public async Task<FinanceSetupStateDto> GetAsync(Guid academicYearId)
	{
		await access.RequireIdentityAsync();
		await access.RequireGuidePermissionAsync();
		return await MapAsync(await access.LoadContextAsync(academicYearId));
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<FinanceSetupStateDto> SaveProgressAsync(Guid academicYearId, FinanceSetupProgressInput input)
	{
		await access.RequireIdentityAsync();
		await access.RequireGuidePermissionAsync();
		if (!FinanceSetupProgress.IsValid(input.CurrentStageKey, input.CurrentSettingKey, input.DeferredSettingKeys) ||
			input.FinishReview && input.CurrentStageKey != "review") throw new BusinessException(FinanceSetupKeys.Invalid);
		var year = await access.LoadContextAsync(academicYearId);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != year.ConcurrencyStamp) throw new AbpDbConcurrencyException();
		var readable = (await access.ReadPermissionsAsync()).Where(x => x.CanView).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
		if (input.CurrentSettingKey != null && !readable.Contains(input.CurrentSettingKey) || input.DeferredSettingKeys.Any(x => !readable.Contains(x)))
			throw new AbpAuthorizationException();
		var hiddenDeferred = FinanceSetupProgress.Read(year).DeferredSettingKeys.Where(x => !readable.Contains(x));
		var deferred = hiddenDeferred.Concat(input.DeferredSettingKeys).Distinct(StringComparer.Ordinal).ToArray();
		FinanceSetupProgress.Save(year, input.CurrentStageKey, input.CurrentSettingKey, deferred, input.FinishReview, CurrentUser.Id!.Value, Clock.Now);
		await years.UpdateAsync(year, autoSave: true);
		return await MapAsync(year);
	}

	private async Task<FinanceSetupStateDto> MapAsync(AcademicYear year)
	{
		var progress = FinanceSetupProgress.Read(year);
		var settings = await access.ReadPermissionsAsync();
		await configuration.ReadAsync(settings, year.Id);
		var readable = settings.Where(x => x.CanView).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
		return new() {
			AcademicYearId = year.Id, ConcurrencyStamp = year.ConcurrencyStamp,
			CurrentStageKey = progress.CurrentStageKey,
			CurrentSettingKey = progress.CurrentSettingKey != null && readable.Contains(progress.CurrentSettingKey) ? progress.CurrentSettingKey : null,
			DeferredSettingKeys = progress.DeferredSettingKeys.Where(readable.Contains).ToArray(), ReviewedAt = progress.ReviewedAt, Settings = settings
		};
	}
}
