using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationProviderConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const string NotConfiguredStatus = "NotConfigured";
	public const string ConfiguredStatus = "Configured";
	public const string DisabledStatus = "Disabled";

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public AdmissionCrmMessageChannel Channel { get; private set; }
	public string Status { get; private set; } = NotConfiguredStatus;
	public bool IsEnabled { get; private set; }
	public string? ProviderName { get; private set; }
	public string? SenderId { get; private set; }
	public string? SettingsJson { get; private set; }
	public string? WebhookUrl { get; private set; }
	public DateTime? LastValidatedAt { get; private set; }

	protected AdmissionCrmCommunicationProviderConfiguration()
	{
	}

	public AdmissionCrmCommunicationProviderConfiguration(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		AdmissionCrmMessageChannel channel)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Channel = channel;
		Status = NotConfiguredStatus;
	}

	public void Update(
		bool isEnabled,
		string? providerName,
		string? senderId,
		string? settingsJson,
		string? webhookUrl,
		DateTime? lastValidatedAt)
	{
		if (isEnabled && string.IsNullOrWhiteSpace(providerName))
		{
			throw new BusinessException("AdmissionCrm:ProviderNameRequired");
		}

		ProviderName = NormalizeOptional(
			providerName,
			nameof(providerName),
			AdmissionCrmConsts.MaxNameLength);
		SenderId = NormalizeOptional(
			senderId,
			nameof(senderId),
			AdmissionCrmConsts.MaxNameLength);
		SettingsJson = NormalizeSettingsJson(settingsJson);
		WebhookUrl = NormalizeOptional(
			webhookUrl,
			nameof(webhookUrl),
			AdmissionCrmConsts.MaxUrlLength);
		IsEnabled = isEnabled && !string.IsNullOrWhiteSpace(ProviderName);
		LastValidatedAt = lastValidatedAt;
		Status = string.IsNullOrWhiteSpace(ProviderName)
			? NotConfiguredStatus
			: IsEnabled
				? ConfiguredStatus
				: DisabledStatus;
	}

	private static string? NormalizeSettingsJson(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		Check.Length(
			value,
			nameof(value),
			AdmissionCrmConsts.MaxRoutingRulesJsonLength);

		using var parsed = JsonDocument.Parse(value);
		if (parsed.RootElement.ValueKind != JsonValueKind.Object)
		{
			throw new BusinessException("AdmissionCrm:ProviderSettingsMustBeObject");
		}

		return value;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
