using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using System.Collections.Generic;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[RemoteService(false)]
public class InitialSetupStatusReader(
	IRepository<InstituteSetupProgress, Guid> progressRepository,
	IRepository<InstituteSetupExtraProperties, Guid> legacyRepository,
	InitialSetupStructureReader structure,
	ITenantRepository tenants) : ShikshaMitraAppService
{
	public Guid RequireTenant()
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null || CurrentUser.TenantId == null ||
			CurrentTenant.Id != CurrentUser.TenantId) throw new AbpAuthorizationException();
		return CurrentUser.TenantId.Value;
	}
	public async Task<bool> CanManageAsync() =>
		await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.BasicInformation.Edit) &&
		await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Add);

	public async Task<InstituteSetupProgress> ReadAsync()
	{
		var tenantId = RequireTenant();
		var progress = await progressRepository.FindAsync(x => x.TenantId == tenantId);
		if (progress != null) return progress;
		var legacy = await legacyRepository.GetListAsync(x => x.TenantId == tenantId);
		progress = new InstituteSetupProgress(tenantId);
		progress.ConcurrencyStamp = "uninitialized";
		// Preserve explicit completion and pre-existing operational schools without inventing grants.
		if (legacy.Any(x => x.State == InstituteSetupState.SetupCompleted) ||
			(legacy.Count == 0 && await structure.IsReadyAsync(tenantId)))
			progress.PreserveLegacyCompletion(Clock.Now);
		else if (legacy.Any(x => x.State >= InstituteSetupState.TenantDetails))
			progress.Advance(0);
		if (!progress.CompletedAt.HasValue && legacy.Any(x => x.State >= InstituteSetupState.AcademicSetup)) progress.Advance(1);
		return progress;
	}
	public async Task<InitialSetupDto> MapAsync(InstituteSetupProgress progress)
	{
		var tenant = await tenants.GetAsync(RequireTenant());
		var canManage = await CanManageAsync();
		var draft = Deserialize(progress.DraftJson);
		if (progress.DraftJson == "{}") draft.Profile = new InitialSetupProfileDraft
		{
			InstituteName = ReadTenantText(tenant, "InstituteName", tenant.Name),
			Email = ReadTenantText(tenant, "Email", ""),
			ContactNumber = ReadTenantText(tenant, "ContactNumber", ""),
			Address1 = ReadTenantText(tenant, "Address1", ""), Address2 = ReadTenantText(tenant, "Address2", ""),
			Address3 = ReadTenantText(tenant, "Address3", ""), City = ReadTenantText(tenant, "City", ""),
			State = ReadTenantText(tenant, "State", ""), Country = ReadTenantText(tenant, "Country", "India"),
			PostalCode = ReadTenantText(tenant, "PostalCode", "")
		};
		return new InitialSetupDto
		{
			IsCompleted = progress.CompletedAt.HasValue, CanManageSetup = canManage,
			CurrentStep = progress.CurrentStep, FurthestStep = progress.FurthestStep,
			ConcurrencyStamp = progress.ConcurrencyStamp, SchoolCode = tenant.Name,
			AcademicYearId = progress.AcademicYearId, DivisionId = progress.DivisionId,
			CompletedAt = progress.CompletedAt, Draft = canManage ? draft : new(),
			CreatedUsers = canManage ? JsonSerializer.Deserialize<List<SetupUserSummary>>(progress.GetProperty<string>("CreatedUsers") ?? "[]")! : new(),
			PendingSteps = canManage ? InitialSetupDraftState.PendingSteps(progress) : []
		};
	}
	public static string ReadTenantText(Volo.Abp.TenantManagement.Tenant tenant, string key, string fallback = "")
	{
		if (!tenant.ExtraProperties.TryGetValue(key, out var value) || value == null) return fallback;
		if (value is string text) return text;
		// Older profile forms saved location objects rather than their display names.
		var element = JsonSerializer.SerializeToElement(value);
		if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? fallback;
		if (element.ValueKind == JsonValueKind.Object)
			foreach (var name in new[] { "name", "Name", "label", "Label" })
				if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
					return property.GetString() ?? fallback;
		return fallback;
	}
	public static InitialSetupDraftDto Deserialize(string json) =>
		JsonSerializer.Deserialize<InitialSetupDraftDto>(json) ?? new();
}
