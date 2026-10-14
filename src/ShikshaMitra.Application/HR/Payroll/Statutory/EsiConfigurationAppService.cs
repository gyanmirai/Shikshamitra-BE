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
	IUserScopeService userScopeService, ShikshaMitra.HR.Payroll.Settings.IEarningComponentRepository earnings) : ApplicationService, IEsiConfigurationAppService
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
		ArgumentNullException.ThrowIfNull(input);
		if (!new SaveEsiConfigurationDtoValidator().Validate(input).IsValid)
			throw new Volo.Abp.Validation.AbpValidationException("Review the ESIC configuration.");
		if (input.CalculationSettings.IsLegacyRead) throw new BusinessException("HR:ESI:SeparateBasesRequired");
		input.CalculationSettings.Validate();
		await resolver.EnsureDefaultAsync();
		await revisions.LockAsync();
		var key = input.IdempotencyKey.ToString("N");
		var fingerprint = key + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
			JsonSerializer.Serialize(new {
				input.EffectiveMonth, input.CalculationSettings, input.Enabled, RegistrationNumber = input.RegistrationNumber?.Trim(),
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
		var month = input.EffectiveMonth == null ? CurrentMonth() :
			DateTime.ParseExact(input.EffectiveMonth, "yyyy-MM", CultureInfo.InvariantCulture);
		if (month < CurrentMonth()) throw new BusinessException("HR:Payroll:EsiConfigurationPastMonth");
		var current = await resolver.ResolveAsync(month);
		if ((input.CalculationSettings.EligibilityComponentId is { } eligibilityId && !await earnings.AnyAsync(x => x.Id == eligibilityId && x.IsActive)) ||
			(input.CalculationSettings.CalculationComponentId is { } calculationId && !await earnings.AnyAsync(x => x.Id == calculationId && x.IsActive)))
			throw new BusinessException("HR:Payroll:EsiComponentInvalid");
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
			current.EffectiveMonth == month ? current.Id : null, treatments, input.CalculationSettings));
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
			SuggestedEffectiveMonth = CurrentMonth().AddMonths(1).ToString("yyyy-MM", CultureInfo.InvariantCulture)
		};
	}
	private static EsiConfigurationVersionDto Map(EsiPayrollConfiguration x) => new() {
		CalculationSettings = x.GetCalculationSettings(), Id = x.Id, EffectiveMonth = x.EffectiveMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
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
		await AuthorizationService.CheckAsync(EsiPermissions.ConfigureCalculation);
		await EnsureHrWideAsync();
	}
	private async Task EnsureHrWideAsync()
	{
		if (!CurrentUser.Id.HasValue || !HrUserScopeAccess.HasHrWideAccess(await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value)))
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
}
