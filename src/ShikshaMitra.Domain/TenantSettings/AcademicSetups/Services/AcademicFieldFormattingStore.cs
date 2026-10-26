using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

public class AcademicFieldFormattingStore(ISettingManager settings) : DomainService
{
	public const string SettingName = "ShikshaMitra.InstituteSetup.AcademicFieldFormatting";
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	public async Task<AcademicFieldFormattingRules> GetAsync()
	{
		if (!CurrentTenant.Id.HasValue) return new();
		var value = await settings.GetOrNullForTenantAsync(SettingName, CurrentTenant.Id.Value, fallback: false);
		if (string.IsNullOrWhiteSpace(value)) return new();
		try
		{
			var rules = JsonSerializer.Deserialize<AcademicFieldFormattingRules>(value, Json) ?? new();
			Validate(rules);
			return rules;
		}
		catch (JsonException)
		{
			throw new AbpValidationException("The school's academic field formatting settings are invalid.");
		}
	}

	public async Task SetAsync(AcademicFieldFormattingRules rules)
	{
		Validate(rules);
		var tenantId = CurrentTenant.GetId();
		await settings.SetForTenantAsync(tenantId, SettingName, JsonSerializer.Serialize(rules, Json));
	}

	public static void Validate(AcademicFieldFormattingRules rules)
	{
		if (rules.Board != AcademicTextCase.Default || !Enum.IsDefined(rules.Medium) || !Enum.IsDefined(rules.Stage)
			|| !Enum.IsDefined(rules.Standard) || !Enum.IsDefined(rules.Division))
			throw new AbpValidationException("Choose a supported text case. Board names must keep their official spelling.");
	}
}
