using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using EsiPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes;

namespace ShikshaMitra.HR.Payroll.Statutory;

[RemoteService(false), Authorize]
public class EsiPolicyAppService(IRepository<EsiPolicy, Guid> policies,
	IRepository<StatutoryRuleVersion, Guid> versions, IPayrollInputRevisionStore fence,
	EsiConfigurationPeriodGuard periodGuard, IUserScopeService scopes,
	IEarningComponentRepository earnings, IRepository<Compensation.EmployeeCompensationRevision, Guid> compensation) : ApplicationService, IEsiPolicyAppService
{
	[Authorize(EsiPermissions.View)]
	public async Task<List<EsiPolicyDto>> GetListAsync()
	{
		await AuthorizationService.CheckAsync(EsiPermissions.View);
		return await ReadAsync();
	}
	public async Task<List<EsiPolicyDto>> GetOptionsAsync()
	{
		if (!await AuthorizationService.IsGrantedAsync(EsiPermissions.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View))
			throw new Volo.Abp.Authorization.AbpAuthorizationException();
		return await ReadAsync();
	}
	[UnitOfWork(isTransactional: true)]
	public virtual async Task<EsiPolicyDto> CreateAsync(CreateEsiPolicyDto input)
	{
		await AuthorizeWriteAsync();
		await fence.LockAsync();
		Validate(input);
		var existing = await versions.FindAsync(VersionId(input.IdempotencyKey));
		if (existing != null) return await ReplayAsync(existing, input);
		if (await policies.AnyAsync(x => x.Code == input.Code.Trim().ToUpperInvariant()))
			throw new BusinessException(HRErrorCodes.EsiPolicyCodeExists);
		var policy = new EsiPolicy(GuidGenerator.Create(), CurrentTenant.Id, input.Name, input.Code);
		await policies.InsertAsync(policy);
		await InsertVersionAsync(policy, input, 1);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return (await ReadAsync()).Single(x => x.Id == policy.Id);
	}
	[UnitOfWork(isTransactional: true)]
	public virtual async Task<EsiPolicyDto> AddVersionAsync(Guid id, EsiPolicyVersionInput input)
	{
		await AuthorizeWriteAsync();
		await fence.LockAsync();
		Validate(input);
		var policy = await policies.GetAsync(id);
		var replay = await versions.FindAsync(VersionId(input.IdempotencyKey));
		if (replay != null)
		{
			if (replay.EsiPolicyId != id) throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
			return await ReplayAsync(replay, input);
		}
		if (policy.IsArchived) throw new BusinessException(HRErrorCodes.EsiPolicyArchived);
		if (policy.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
		var history = await versions.GetListAsync(x => x.EsiPolicyId == id);
		if (history.Any(x => x.EffectiveFrom >= input.EffectiveFrom.Date))
			throw new BusinessException(HRErrorCodes.EsiPolicyVersionOverlap);
		await InsertVersionAsync(policy, input, history.Select(x => x.Version).DefaultIfEmpty().Max() + 1);
		await policies.UpdateAsync(policy);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return (await ReadAsync()).Single(x => x.Id == id);
	}
	[UnitOfWork(isTransactional: true)]
	public virtual async Task ArchiveAsync(Guid id, string concurrencyStamp)
	{
		await AuthorizeWriteAsync();
		await fence.LockAsync();
		var policy = await policies.GetAsync(id);
		if (policy.IsArchived) return;
		if (policy.ConcurrencyStamp != concurrencyStamp) throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
		policy.Archive();
		await policies.UpdateAsync(policy);
	}
	private async Task InsertVersionAsync(EsiPolicy policy, EsiPolicyVersionInput input, int number)
	{
		var assigned = (await compensation.GetListAsync(x => x.EsiPolicyId == policy.Id && x.Status == Compensation.CompensationRevisionStatus.Approved)).Select(x => x.EmployeeId).Distinct().ToList();
		if (assigned.Count > 0) await periodGuard.InvalidateAsync(input.EffectiveFrom.Date, null, assigned);
		var settings = input.CalculationSettings;
		settings.Validate();
		if ((settings.EligibilityComponentId.HasValue && !await earnings.AnyAsync(x => x.Id == settings.EligibilityComponentId && x.IsActive)) ||
			(settings.CalculationComponentId.HasValue && !await earnings.AnyAsync(x => x.Id == settings.CalculationComponentId && x.IsActive)))
			throw new BusinessException(HRErrorCodes.PayrollComponentInactive);
		var version = new StatutoryRuleVersion(VersionId(input.IdempotencyKey), CurrentTenant.Id, "ESI", "IN-GJ", number,
			input.EffectiveFrom, null, input.EstablishmentThreshold, input.StandardWageCeiling, input.DisabilityWageCeiling,
			input.EmployeePercentage, input.EmployerPercentage, input.ExclusionCapPercentage, input.InKindCapPercentage,
			Enum.Parse<StatutoryRoundingPolicy>(input.RoundingPolicy), input.SourceUrl, input.SourceVerifiedOn,
			esiPolicyId: policy.Id, calculationSettings: settings, employeeShareDailyWageExemption: input.EmployeeShareDailyWageExemption);
		version.SetRequestFingerprint(Fingerprint(input));
		await versions.InsertAsync(version);
	}
	private async Task<EsiPolicyDto> ReplayAsync(StatutoryRuleVersion version, EsiPolicyVersionInput input)
	{
		if (version.RequestFingerprint != Fingerprint(input)) throw new BusinessException(HRErrorCodes.StatutoryPreviewStale);
		return (await ReadAsync()).Single(x => x.Id == version.EsiPolicyId);
	}
	private static string Fingerprint(EsiPolicyVersionInput input) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, input.GetType())));
	private Guid VersionId(Guid key) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{CurrentTenant.Id:N}:{key:N}"))[..16]);
	private static void Validate(EsiPolicyVersionInput input)
	{
		if (!new EsiPolicyVersionInputValidator().Validate(input).IsValid || input.CalculationSettings.IsLegacyRead || input.CalculationSettings.HasRateOverride)
			throw new BusinessException(HRErrorCodes.EsiPolicyInvalid);
	}
	private async Task AuthorizeWriteAsync()
	{
		await AuthorizationService.CheckAsync(EsiPermissions.Edit);
		await AuthorizationService.CheckAsync(EsiPermissions.Activate);
		await AuthorizationService.CheckAsync(EsiPermissions.ConfigureCalculation);
		if (CurrentTenant.Id == null || !CurrentUser.Id.HasValue ||
			!HrUserScopeAccess.HasHrWideAccess(await scopes.GetUserScopeAsync(CurrentUser.Id.Value)))
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
	private async Task<List<EsiPolicyDto>> ReadAsync()
	{
		var allVersions = await versions.GetListAsync(x => x.SchemeCode == "ESI" && x.EsiPolicyId != null);
		return (await policies.GetListAsync()).OrderBy(x => x.Name).Select(policy => new EsiPolicyDto
		{
			Id = policy.Id, Name = policy.Name, Code = policy.Code, IsDefault = policy.IsDefault,
			IsArchived = policy.IsArchived, ConcurrencyStamp = policy.ConcurrencyStamp,
			Versions = allVersions.Where(x => x.EsiPolicyId == policy.Id).OrderByDescending(x => x.EffectiveFrom).Select(version =>
			{
				var next = allVersions.Where(x => x.EsiPolicyId == policy.Id && x.EffectiveFrom > version.EffectiveFrom)
					.OrderBy(x => x.EffectiveFrom).FirstOrDefault();
				return new EsiPolicyVersionDto { Id = version.Id, Version = version.Version, EffectiveFrom = version.EffectiveFrom,
					EffectiveTo = version.EffectiveTo, AppliesUntil = next?.EffectiveFrom.AddDays(-1) ?? version.EffectiveTo,
					SchemeCode = version.SchemeCode, JurisdictionCode = version.JurisdictionCode,
					EmployeePercentage = version.EmployeePercentage, EmployerPercentage = version.EmployerPercentage,
					StandardWageCeiling = version.StandardWageCeiling, DisabilityWageCeiling = version.DisabilityWageCeiling,
					EmployeeShareDailyWageExemption = version.EmployeeShareDailyWageExemption,
					EstablishmentThreshold = version.EstablishmentThreshold, ExclusionCapPercentage = version.ExclusionCapPercentage,
					InKindCapPercentage = version.InKindCapPercentage, RoundingPolicy = version.RoundingPolicy.ToString(),
					SourceUrl = version.SourceUrl, SourceVerifiedOn = version.SourceVerifiedOn, CalculationSettings = version.GetPolicySettings() };
			}).ToList()
		}).ToList();
	}
}
