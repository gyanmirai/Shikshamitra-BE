using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[RemoteService(false)]
public class InitialSetupProfileWriter(ITenantRepository tenants, InitialSetupStatusReader reader) : ShikshaMitraAppService
{
	public async Task<InitialSetupProfileDraft> WriteAsync(SaveSetupProfileInput input)
	{
		var tenantId = reader.RequireTenant();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.BasicInformation.Edit);
		var tenant = await tenants.GetAsync(tenantId);
		// Only editable profile fields; school code, status, credentials and infrastructure stay unchanged.
		var profile = JsonSerializer.Deserialize<InitialSetupProfileDraft>(JsonSerializer.Serialize(input))!;
		foreach (var property in typeof(InitialSetupProfileDraft).GetProperties())
		{
			var value = ((string?)property.GetValue(profile) ?? "").Trim();
			property.SetValue(profile, value);
			tenant.SetProperty(property.Name, value);
		}
		await tenants.UpdateAsync(tenant);
		return profile;
	}
}
