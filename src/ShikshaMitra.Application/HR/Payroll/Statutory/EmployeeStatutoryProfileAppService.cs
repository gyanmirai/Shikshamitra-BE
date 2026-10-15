using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using PfPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions;
using EsiPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes;

namespace ShikshaMitra.HR.Payroll.Statutory;

[RemoteService(false)]
public class EmployeeStatutoryProfileAppService(IRepository<EmployeeStatutoryProfileVersion, Guid> versions,
	EmployeeStatutoryProfileResolver resolver, IEmployeeAuthorizationService access,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots, IRepository<PayrollRunReview, Guid> reviews,
	IRepository<SalarySlip, Guid> slips, IPayrollInputRevisionStore fence, IClock clock, PayrollScopedEmployeeResolver employeeScope)
	: ApplicationService, IEmployeeStatutoryProfileAppService
{
	public async Task<PayrollRegistrationReadinessDto> GetReadinessAsync(string? month = null, int page = 1, int pageSize = 25, string? search = null)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View);
		if (!CurrentUser.Id.HasValue) throw new BusinessException("HR:Payroll:StatutoryProfileInvalid");
		var effective = ParseMonth(month); var monthKey = effective.ToString("yyyy-MM");
		var locked = await snapshots.GetQueryableAsync(); var issued = await slips.GetQueryableAsync();
		var query = (await employeeScope.GetScopedEmployeeQueryAsync(includeUnknownStageEmployees: true)).Where(employee =>
			!locked.Any(snapshot => snapshot.EmployeeId == employee.Id && snapshot.MonthYear == monthKey && snapshot.IsLocked) &&
			!issued.Any(slip => slip.EmployeeId == employee.Id && slip.MonthYear == monthKey && (slip.Status == SalarySlipStatus.Finalized || slip.Status == SalarySlipStatus.Paid)));
		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim();
			query = query.Where(x => (x.FirstName ?? "").Contains(term) || (x.LastName ?? "").Contains(term) || (x.EmployeeCode ?? "").Contains(term));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var size = Math.Clamp(pageSize, 1, 100); var offset = checked((Math.Clamp(page, 1, 100000) - 1) * size);
		var staff = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.EmployeeCode).ThenBy(x => x.Id).Skip(offset).Take(size)
			.Select(x => new { x.Id, x.FirstName, x.LastName, x.EmployeeCode }));
		var profiles = await resolver.ResolveAsync(effective, staff.Select(x => x.Id).ToList());
		return new() { TotalCount = total, Items = staff.Select(x =>
		{
			var profile = profiles[x.Id];
			return new PayrollRegistrationReadinessRowDto(x.Id, $"{x.FirstName} {x.LastName}".Trim(), x.EmployeeCode, profile.ApplyPf,
				PayrollSchemeApplication.HasNumber(profile.PfNumber), profile.ApplyEsic, PayrollSchemeApplication.HasNumber(profile.EsicInsuranceNumber),
				profile.VersionId.HasValue ? profile.EffectiveMonth.ToString("yyyy-MM") : monthKey);
		}).ToList() };
	}
	public async Task<EmployeeStatutoryProfileDto> GetAsync(Guid employeeId, string? month = null)
	{
		await access.CheckEmployeeScopeAccessAsync(employeeId);
		var requested = ParseMonth(month);
		var effective = month == null ? await EditableMonthAsync(employeeId, requested) : requested;
		return await ProjectAsync(employeeId, effective);
	}
	[UnitOfWork(true)]
	public virtual async Task<EmployeeStatutoryProfileDto> SaveAsync(Guid employeeId, SaveEmployeeStatutoryProfileDto input)
	{
		ArgumentNullException.ThrowIfNull(input);
		if (input.Profile == null || input.IdempotencyKey == Guid.Empty || (!input.UpdatePf && !input.UpdateEsic)) throw new BusinessException("HR:Payroll:StatutoryProfileInvalid");
		if (input.UpdatePf) await access.CheckEmployeeScopedPermissionAsync(employeeId, PfPermissions.Manage);
		if (input.UpdateEsic) await access.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.ManageEnrollment);
		var requested = ParseMonth(input.EffectiveMonth);
		if (requested < CurrentMonth()) throw new BusinessException("HR:Payroll:StatutoryProfilePastMonth");
		await fence.LockAsync();
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
		var duplicate = await versions.FindAsync(x => x.EmployeeId == employeeId && x.RequestId == input.IdempotencyKey);
		if (duplicate != null)
		{
			if (duplicate.RequestFingerprint != fingerprint) throw new BusinessException("HR:Payroll:StatutoryProfileConflict");
			return await ProjectAsync(employeeId, duplicate.EffectiveMonth, savedVersion: duplicate);
		}
		var effective = await EditableMonthAsync(employeeId, requested);
		var current = (await versions.GetListAsync(x => x.EmployeeId == employeeId && !x.IsSuperseded && x.EffectiveMonth <= effective))
			.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault();
		if (current?.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException("HR:Payroll:StatutoryProfileConflict");
		var previous = await resolver.ResolveAsync(employeeId, effective);
		var merged = previous with
		{
			ApplyPf = input.UpdatePf ? input.Profile.ApplyPf : previous.ApplyPf,
			PfNumber = input.UpdatePf ? input.Profile.PfNumber : previous.PfNumber,
			UanNumber = input.UpdatePf ? input.Profile.UanNumber : previous.UanNumber,
			PfDisabledReason = input.UpdatePf ? input.Profile.PfDisabledReason : previous.PfDisabledReason,
			ApplyEsic = input.UpdateEsic ? input.Profile.ApplyEsic : previous.ApplyEsic,
			EsicInsuranceNumber = input.UpdateEsic ? input.Profile.EsicInsuranceNumber : previous.EsicInsuranceNumber,
			EsicDisabledReason = input.UpdateEsic ? input.Profile.EsicDisabledReason : previous.EsicDisabledReason
		};
		EmployeeStatutoryProfileVersion.Validate(merged);
		if (current?.EffectiveMonth == effective)
		{
			current.Supersede();
			await versions.UpdateAsync(current, autoSave: true);
		}
		await versions.InsertAsync(new EmployeeStatutoryProfileVersion(GuidGenerator.Create(), CurrentTenant.Id, employeeId, effective, merged, input.IdempotencyKey, fingerprint), autoSave: true);
		var issued = (await slips.GetListAsync(x => x.EmployeeId == employeeId && (x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid)))
			.Select(x => x.MonthYear).ToHashSet();
		var unlocked = (await snapshots.GetListAsync(x => x.EmployeeId == employeeId && !x.IsLocked))
			.Where(x => ParseMonth(x.MonthYear) >= effective && !issued.Contains(x.MonthYear)).ToList();
		foreach (var item in unlocked)
		{
			await reviews.DeleteAsync(x => x.EmployeeId == employeeId && x.MonthYear == item.MonthYear);
			await snapshots.DeleteAsync(item);
		}
		return await ProjectAsync(employeeId, effective);
	}
	internal async Task<PayrollStatutoryProfile> ValidateDraftAsync(Guid employeeId, DateTime month,
		PayrollStatutoryProfile draft, string? stamp, bool updatePf, bool updateEsic)
	{
		await access.CheckEmployeeScopeAccessAsync(employeeId);
		var current = await resolver.ResolveAsync(employeeId, month);
		var merged = current with
		{
			ApplyPf = updatePf ? draft.ApplyPf : current.ApplyPf, PfNumber = updatePf ? draft.PfNumber : current.PfNumber,
			UanNumber = updatePf ? draft.UanNumber : current.UanNumber, PfDisabledReason = updatePf ? draft.PfDisabledReason : current.PfDisabledReason,
			ApplyEsic = updateEsic ? draft.ApplyEsic : current.ApplyEsic,
			EsicInsuranceNumber = updateEsic ? draft.EsicInsuranceNumber : current.EsicInsuranceNumber,
			EsicDisabledReason = updateEsic ? draft.EsicDisabledReason : current.EsicDisabledReason
		};
		EmployeeStatutoryProfileVersion.Validate(merged);
		if (updatePf && (merged.ApplyPf != current.ApplyPf || merged.PfNumber != current.PfNumber || merged.UanNumber != current.UanNumber || merged.PfDisabledReason != current.PfDisabledReason))
			await access.CheckEmployeeScopedPermissionAsync(employeeId, PfPermissions.Manage);
		if (updateEsic && (merged.ApplyEsic != current.ApplyEsic || merged.EsicInsuranceNumber != current.EsicInsuranceNumber || merged.EsicDisabledReason != current.EsicDisabledReason))
			await access.CheckEmployeeScopedPermissionAsync(employeeId, EsiPermissions.ManageEnrollment);
		if ((current.VersionId.HasValue ? (await versions.GetAsync(current.VersionId.Value)).ConcurrencyStamp : null) != stamp)
			throw new BusinessException("HR:Payroll:StatutoryProfileConflict");
		return merged;
	}

	internal async Task<EmployeeStatutoryProfileDto> GetProjectionAsync(Guid employeeId)
	{
		await access.CheckEmployeeScopeAccessAsync(employeeId);
		return await ProjectAsync(employeeId, CurrentMonth(), requireSchemeAccess: false);
	}
	private async Task<EmployeeStatutoryProfileDto> ProjectAsync(Guid employeeId, DateTime month, bool requireSchemeAccess = true, EmployeeStatutoryProfileVersion? savedVersion = null)
	{
		var pfManage = await AuthorizationService.IsGrantedAsync(PfPermissions.Manage);
		var pfView = pfManage || await AuthorizationService.IsGrantedAsync(PfPermissions.View);
		var esiManage = await AuthorizationService.IsGrantedAsync(EsiPermissions.ManageEnrollment);
		var esiView = esiManage || await AuthorizationService.IsGrantedAsync(EsiPermissions.View);
		if (requireSchemeAccess && !pfView && !esiView) await AuthorizationService.CheckAsync(PfPermissions.View);
		var profile = savedVersion?.ToProfile() ?? await resolver.ResolveAsync(employeeId, month);
		var version = savedVersion ?? (profile.VersionId.HasValue ? await versions.GetAsync(profile.VersionId.Value) : null);
		var dto = new EmployeeStatutoryProfileDto
		{
			EmployeeId = employeeId, EffectiveMonth = month.ToString("yyyy-MM"), ConcurrencyStamp = version?.ConcurrencyStamp,
			CanViewPf = pfView, CanViewEsic = esiView, CanManagePf = pfManage, CanManageEsic = esiManage,
			Profile = profile with { PfNumber = pfView ? profile.PfNumber : null, UanNumber = pfView ? profile.UanNumber : null,
				PfDisabledReason = pfView ? profile.PfDisabledReason : null, EsicInsuranceNumber = esiView ? profile.EsicInsuranceNumber : null, EsicDisabledReason = esiView ? profile.EsicDisabledReason : null }
		};
		if (pfView) dto.Warnings.AddRange(profile.ApplyPf ? PayrollSchemeApplication.HasNumber(profile.PfNumber) ? [] : new[] { "PF_NUMBER_MISSING" } : new[] { "PF_APPLY_DISABLED" });
		if (esiView) dto.Warnings.AddRange(profile.ApplyEsic ? PayrollSchemeApplication.HasNumber(profile.EsicInsuranceNumber) ? [] : new[] { "ESI_NUMBER_MISSING" } : new[] { "ESI_APPLY_DISABLED" });
		if (await AuthorizationService.IsGrantedAsync(PfPermissions.ViewAudit))
			dto.History = (await versions.GetListAsync(x => x.EmployeeId == employeeId)).OrderByDescending(x => x.CreationTime)
				.Select(x => new EmployeeStatutoryProfileHistoryDto(x.Id, x.EffectiveMonth.ToString("yyyy-MM"), x.ApplyPf, x.ApplyEsic,
					PayrollSchemeApplication.HasNumber(x.PfNumber), PayrollSchemeApplication.HasNumber(x.EsicInsuranceNumber),
					pfView ? x.PfDisabledReason : null, esiView ? x.EsicDisabledReason : null, x.CreationTime)).ToList();
		return dto;
	}
	private async Task<DateTime> EditableMonthAsync(Guid employeeId, DateTime requested)
	{
		var locked = (await snapshots.GetListAsync(x => x.EmployeeId == employeeId && x.IsLocked)).Select(x => x.MonthYear)
			.Concat((await slips.GetListAsync(x => x.EmployeeId == employeeId && (x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid))).Select(x => x.MonthYear)).ToHashSet();
		while (locked.Contains(requested.ToString("yyyy-MM"))) requested = requested.AddMonths(1);
		return requested;
	}
	private DateTime CurrentMonth()
	{
		var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.Now, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		return new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
	}
	private DateTime ParseMonth(string? month)
	{
		if (month == null) return CurrentMonth();
		if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw new BusinessException("HR:Payroll:StatutoryProfileInvalid");
		return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
	}
}
