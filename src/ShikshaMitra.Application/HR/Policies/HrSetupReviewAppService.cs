using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Policies;

[Authorize, RemoteService(false)]
public class HrSetupReviewAppService(
	IRepository<AcademicYear, Guid> years,
	IIdentityUserRepository users,
	ITenantRepository tenants,
	HrSetupReviewConfigurationReader configuration,
	HrSetupReviewSampleReader sample,
	IPermissionChecker permissionChecker,
	IUnitOfWorkManager uows) : ApplicationService, IHrSetupReviewAppService
{
	public async Task<HrSetupReviewDto> GetAsync(Guid academicYearId, string section, HrSetupReviewQuery input)
	{
		await EnsurePermissionAsync(section, write: false);
		using var unit = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var year = await LoadYearAsync(academicYearId);
		var result = await ReadAsync(year, section, input);
		await unit.CompleteAsync();
		return result;
	}

	public async Task<HrSetupReviewDto> RecordAsync(Guid academicYearId, string section, RecordHrSetupReviewDto input)
	{
		await EnsurePermissionAsync(section, write: true);
		// Serializable snapshot + AcademicYear's existing optimistic concurrency token prevent lost
		// acknowledgements. Configuration edits ordered after this transaction visibly stale the record.
		using var unit = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var year = await LoadYearAsync(academicYearId);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != year.ConcurrencyStamp)
			throw new BusinessException(HrSetupReviewSections.Conflict);
		var result = await ReadAsync(year, section, input);
		if (string.IsNullOrWhiteSpace(input.Fingerprint) || input.Fingerprint != result.Fingerprint)
			throw new BusinessException(HrSetupReviewSections.Conflict);
		if (result.ConfigurationState != "valid") throw new BusinessException(HrSetupReviewSections.Invalid);
		AcademicYearHrSetupReview.Record(year, section, new(CurrentUser.Id!.Value,
			new DateTimeOffset(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc)), result.Fingerprint, result.EmployeeId, result.Date));
		await years.UpdateAsync(year, autoSave: true);
		var saved = await ReadAsync(year, section, input);
		await unit.CompleteAsync();
		return saved;
	}

	private async Task<AcademicYear> LoadYearAsync(Guid id)
	{
		if (CurrentTenant.Id is not Guid tenantId || CurrentUser.Id is not Guid actorId) throw new AbpAuthorizationException();
		var actor = await users.FindAsync(actorId);
		if (actor == null || actor.TenantId != tenantId || !actor.IsActive) throw new AbpAuthorizationException();
		return await years.FindAsync(x => x.Id == id && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(AcademicYear), id);
	}

	private async Task<HrSetupReviewDto> ReadAsync(AcademicYear year, string section, HrSetupReviewQuery input)
	{
		var document = AcademicYearHrSetupReview.Read(year);
		document.Sections.TryGetValue(section, out var entry);
		// Reopen the recorded sample so another reader does not silently substitute a different check.
		var recordedSample = section == HrSetupReviewSections.Sample && !input.NewSample && !input.EmployeeId.HasValue && !input.Date.HasValue && entry != null;
		if (recordedSample)
			input = new() { EmployeeId = entry!.EmployeeId, Date = entry.Date };
		var result = section == HrSetupReviewSections.Sample
			? await sample.ReadAsync(year, input, recordedSample) : await configuration.ReadAsync(year, section);
		result.ConcurrencyStamp = year.ConcurrencyStamp;
		result.StarterInitialized = year.GetProperty(HrStarterPolicyDefaults.YearInitializedProperty, false);
		var tenant = await tenants.GetAsync(year.TenantId!.Value);
		result.TenantStarterEnabled = tenant.GetProperty(HrStarterPolicyDefaults.TenantEnabledProperty, false);
		result.ReviewedBy = entry?.ActorId;
		if (entry != null)
		{
			var actor = await users.FindAsync(entry.ActorId);
			result.ReviewedByName = actor?.TenantId == year.TenantId ? actor.Name ?? actor.UserName : "Previous school account";
		}
		result.ReviewedAt = entry?.ReviewedAt;
		result.ReviewState = entry == null ? "record_not_present" : entry.Fingerprint == result.Fingerprint && result.ConfigurationState == "valid" ? "reviewed" : "stale";
		result.CanReview = result.ConfigurationState == "valid" && await permissionChecker.IsGrantedAsync(WritePermission(section));
		return result;
	}

	private async Task EnsurePermissionAsync(string section, bool write)
	{
		var read = section switch {
			HrSetupReviewSections.Calendar or HrSetupReviewSections.Attendance or HrSetupReviewSections.Payroll or HrSetupReviewSections.Sample => ShikshaMitraSubmodulePermissions.HR.Policies.View,
			HrSetupReviewSections.Leave => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View,
			_ => throw new BusinessException(HrSetupReviewSections.Invalid)
		};
		if (!await permissionChecker.IsGrantedAsync(read) || (write && !await permissionChecker.IsGrantedAsync(WritePermission(section))) ||
			(section == HrSetupReviewSections.Sample && !await permissionChecker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View)))
			throw new AbpAuthorizationException();
	}

	private static string WritePermission(string section) => section switch {
		HrSetupReviewSections.Leave => ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.Edit,
		HrSetupReviewSections.Sample => ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact,
		_ => ShikshaMitraSubmodulePermissions.HR.Policies.Edit
	};
}
