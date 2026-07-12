using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmHousehold : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string PreferredLanguage { get; private set; } = "en";
	public string AddressesJson { get; private set; } = "[]";
	public string CommunicationPreferencesJson { get; private set; } = "{}";
	public bool IsActive { get; private set; }

	protected AdmissionCrmHousehold()
	{
	}

	public AdmissionCrmHousehold(
		Guid id, Guid? tenantId, string code, string displayName, string preferredLanguage,
		string addressesJson, string communicationPreferencesJson) : base(id)
	{
		TenantId = tenantId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxHouseholdCodeLength);
		IsActive = true;
		Update(displayName, preferredLanguage, addressesJson, communicationPreferencesJson);
	}

	public void Update(
		string displayName, string preferredLanguage, string addressesJson,
		string communicationPreferencesJson)
	{
		DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), AdmissionCrmConsts.MaxNameLength);
		PreferredLanguage = Check.NotNullOrWhiteSpace(preferredLanguage, nameof(preferredLanguage), AdmissionCrmConsts.MaxLanguageCodeLength);
		AddressesJson = ValidateJson(addressesJson, JsonValueKind.Array, nameof(addressesJson));
		CommunicationPreferencesJson = ValidateJson(communicationPreferencesJson, JsonValueKind.Object, nameof(communicationPreferencesJson));
	}

	public void Deactivate() => IsActive = false;

	private static string ValidateJson(string json, JsonValueKind kind, string name)
	{
		var value = Check.NotNullOrWhiteSpace(json, name, AdmissionCrmConsts.MaxHouseholdJsonLength);
		using var document = JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != kind)
		{
			throw new BusinessException("AdmissionCrm:InvalidHouseholdJson").WithData("Field", name);
		}
		return value;
	}
}
