using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.Formatting;

public class InstituteFieldFormattingStore(ISettingManager settings) : DomainService
{
	public const string SettingPrefix = "ShikshaMitra.InstituteSetup.FieldFormatting.";
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	public async Task<Dictionary<string, AcademicTextCase>> GetAsync(string scope)
	{
		var defaults = InstituteFieldFormattingCatalog.Defaults(scope);
		if (!CurrentTenant.Id.HasValue) return defaults;
		var value = await settings.GetOrNullForTenantAsync(SettingPrefix + scope, CurrentTenant.Id.Value, fallback: false);
		if (string.IsNullOrWhiteSpace(value)) return defaults;
		try
		{
			var rules = JsonSerializer.Deserialize<Dictionary<string, AcademicTextCase>>(value, Json);
			InstituteFieldFormattingCatalog.Validate(scope, rules);
			return rules!;
		}
		catch (JsonException)
		{
			throw new AbpValidationException("The school's field formatting settings are invalid.");
		}
	}

	public async Task SetAsync(string scope, Dictionary<string, AcademicTextCase> rules)
	{
		InstituteFieldFormattingCatalog.Validate(scope, rules);
		await settings.SetForTenantAsync(CurrentTenant.GetId(), SettingPrefix + scope, JsonSerializer.Serialize(rules, Json));
	}
}
