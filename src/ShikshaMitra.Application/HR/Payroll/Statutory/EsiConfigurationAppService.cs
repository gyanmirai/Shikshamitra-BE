using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using EsiPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes;

namespace ShikshaMitra.HR.Payroll.Statutory;

[RemoteService(false)]
[Authorize]
public class EsiConfigurationAppService(IRepository<EsiPayrollConfiguration, Guid> configurations,
	EsiPayrollConfigurationResolver resolver, EsiStatutoryAppService statutoryService,
	EsiConfigurationPeriodGuard periodGuard, IPayrollInputRevisionStore revisions,
	IUserScopeService userScopeService) : ApplicationService, IEsiConfigurationAppService
{
	[Authorize(EsiPermissions.View)]
	public async Task<EsiConfigurationSettingsDto> GetAsync()
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		await resolver.EnsureDefaultAsync();
		return await ReadAsync();
	}
	[Authorize(EsiPermissions.Edit), Authorize(EsiPermissions.Activate)]
	[UnitOfWork(isTransactional: true)]
	public async Task<EsiConfigurationSettingsDto> SaveAsync(SaveEsiConfigurationDto input)
	{
		await AuthorizeMutationAsync();
		if (!new SaveEsiConfigurationDtoValidator().Validate(input).IsValid)
			throw new Volo.Abp.Validation.AbpValidationException("Review the ESIC configuration.");
		await resolver.EnsureDefaultAsync();
		await revisions.LockAsync();
		var key = input.IdempotencyKey.ToString("N");
		var fingerprint = key + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
			JsonSerializer.Serialize(new {
				input.Enabled, RegistrationNumber = input.RegistrationNumber?.Trim(),
				CoveredSince = input.CoveredSince?.Date, input.ConcurrencyStamp,
				Treatments = input.Treatments.OrderBy(x => x.ComponentId)
			}))))[..32];
		var previous = await configurations.FindAsync(x => x.RequestFingerprint.StartsWith(key));
		if (previous != null)
		{
			if (previous.RequestFingerprint != fingerprint)
				throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
			return await ReadAsync();
		}
		var month = CurrentMonth();
		var current = await resolver.ResolveAsync(month);
		if (current == null || current.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
		var catalog = await statutoryService.ReadComponentTreatmentsAsync(month);
		if (input.Treatments.Any(x => catalog.All(c => c.ComponentId != x.ComponentId)))
			throw new BusinessException(HRErrorCodes.PayrollComponentInactive);
		var treatments = current.GetTreatments();
		foreach (var item in input.Treatments)
			treatments[item.ComponentId] = Enum.Parse<StatutoryWageTreatment>(item.Treatment);
		if (current.EffectiveMonth == month)
		{
			current.Cancel("Superseded by a saved configuration for the same month.");
			await configurations.UpdateAsync(current, autoSave: true);
		}
		await configurations.InsertAsync(new EsiPayrollConfiguration(GuidGenerator.Create(), CurrentTenant.Id,
			month, input.Enabled, input.RegistrationNumber, input.CoveredSince, fingerprint,
			current.EffectiveMonth == month ? current.Id : null, treatments));
		await periodGuard.InvalidateAsync(month, null);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await ReadAsync();
	}
	private async Task<EsiConfigurationSettingsDto> ReadAsync()
	{
		var history = (await configurations.GetListAsync()).OrderByDescending(x => x.EffectiveMonth)
			.ThenByDescending(x => x.CreationTime).ToList();
		return new EsiConfigurationSettingsDto {
			HasSchoolWideScope = CurrentUser.Id.HasValue &&
				HrUserScopeAccess.HasHrWideAccess(await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value)),
			Current = history.Where(x => !x.IsCancelled && x.EffectiveMonth <= CurrentMonth()).Select(Map).FirstOrDefault(),
			History = history.Select(Map).ToList(),
			SuggestedEffectiveMonth = CurrentMonth().ToString("yyyy-MM", CultureInfo.InvariantCulture)
		};
	}
	private static EsiConfigurationVersionDto Map(EsiPayrollConfiguration x) => new() {
		Id = x.Id, EffectiveMonth = x.EffectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
		Enabled = x.Enabled, RegistrationNumber = x.RegistrationNumber, CoveredSince = x.CoveredSince,
		IsCancelled = x.IsCancelled, CancellationReason = x.CancellationReason, ConcurrencyStamp = x.ConcurrencyStamp
	};
	private DateTime CurrentMonth()
	{
		// Gujarat-only payroll uses Indian local month boundaries, not the host timezone.
		var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc),
			TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		return new DateTime(local.Year, local.Month, 1);
	}
	private async Task AuthorizeMutationAsync()
	{
		await AuthorizationService.CheckAsync(EsiPermissions.Edit);
		await AuthorizationService.CheckAsync(EsiPermissions.Activate);
		await EnsureHrWideAsync();
	}
	private async Task EnsureHrWideAsync()
	{
		if (!CurrentUser.Id.HasValue || !HrUserScopeAccess.HasHrWideAccess(await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value)))
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
}
