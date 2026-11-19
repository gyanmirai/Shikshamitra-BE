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

namespace ShikshaMitra.Academic.Setup;

[Authorize, RemoteService(false)]
public class AcademicSetupAppService(
	AcademicSetupAccess access, AcademicSetupVisitService visits,
	AcademicSetupConfigurationReader configuration, IRepository<AcademicYear, Guid> years) : ApplicationService, IAcademicSetupAppService
{
	public Task<AcademicSetupVisitStatusDto> GetVisitStatusAsync() => visits.GetAsync();
	public Task<AcademicSetupVisitDto> RecordVisitAsync(AcademicSetupVisitInput input) => visits.RecordAsync(input);

	public async Task<AcademicSetupStateDto> GetAsync(Guid academicYearId, AcademicSetupContextInput input)
	{
		await access.RequireIdentityAsync();
		await access.RequireVisitPermissionAsync(null);
		return await MapAsync(await access.LoadContextAsync(academicYearId, input));
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<AcademicSetupStateDto> SaveProgressAsync(Guid academicYearId, AcademicSetupProgressInput input)
	{
		await access.RequireIdentityAsync();
		await access.RequireVisitPermissionAsync(null);
		if (!AcademicSetupProgress.IsValid(input.CurrentStageKey, input.CurrentSettingKey, input.DeferredSettingKeys) ||
			input.FinishReview && input.CurrentStageKey != "review") throw new BusinessException(AcademicSetupKeys.Invalid);
		var context = await access.LoadContextAsync(academicYearId, input);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != context.Year.ConcurrencyStamp) throw new AbpDbConcurrencyException();
		var readable = (await access.ReadPermissionsAsync()).Where(x => x.CanView).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
		if (input.CurrentSettingKey != null && !readable.Contains(input.CurrentSettingKey) || input.DeferredSettingKeys.Any(x => !readable.Contains(x)))
			throw new AbpAuthorizationException();
		var hiddenDeferred = AcademicSetupProgress.Read(context.Year).DeferredSettingKeys.Where(x => !readable.Contains(x));
		var deferred = hiddenDeferred.Concat(input.DeferredSettingKeys).Distinct(StringComparer.Ordinal).ToArray();
		AcademicSetupProgress.Save(context.Year, input.CurrentStageKey, input.CurrentSettingKey, deferred, input.FinishReview, CurrentUser.Id!.Value, Clock.Now);
		await years.UpdateAsync(context.Year, autoSave: true);
		return await MapAsync(context);
	}

	private async Task<AcademicSetupStateDto> MapAsync(AcademicSetupAccess.Context context)
	{
		var progress = AcademicSetupProgress.Read(context.Year);
		var settings = await access.ReadPermissionsAsync();
		await configuration.ReadAsync(settings, context);
		var readable = settings.Where(x => x.CanView).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
		return new() {
			AcademicYearId = context.Year.Id, ConcurrencyStamp = context.Year.ConcurrencyStamp,
			CurrentStageKey = progress.CurrentStageKey,
			CurrentSettingKey = progress.CurrentSettingKey != null && readable.Contains(progress.CurrentSettingKey) ? progress.CurrentSettingKey : null,
			DeferredSettingKeys = progress.DeferredSettingKeys.Where(readable.Contains).ToArray(), ReviewedAt = progress.ReviewedAt, Settings = settings
		};
	}
}
