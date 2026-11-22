using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppTemplateBinding : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TemplateRevisionId { get; private set; }
	public Guid ProviderSettingId { get; private set; }
	public string ProviderTemplateName { get; private set; } = string.Empty;
	public string? ProviderTemplateId { get; private set; }
	public string LanguageCode { get; private set; } = "en";
	public string ParameterMappingsJson { get; private set; } = "[]";
	public NotificationProviderTemplateState ProviderStatus { get; private set; }
	public DateTimeOffset? LastSyncedAt { get; private set; }
	protected WhatsAppTemplateBinding() { }

	public WhatsAppTemplateBinding(Guid id, Guid tenantId, Guid templateRevisionId, Guid providerSettingId,
		string providerTemplateName, string languageCode, string parameterMappingsJson) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		TemplateRevisionId = NotificationDataGuard.Identifier(templateRevisionId, nameof(templateRevisionId));
		ProviderSettingId = NotificationDataGuard.Identifier(providerSettingId, nameof(providerSettingId));
		ProviderTemplateName = NotificationDataGuard.Text(providerTemplateName, nameof(providerTemplateName), 160);
		LanguageCode = NotificationDataGuard.Text(languageCode, nameof(languageCode), 16).ToLowerInvariant();
		ParameterMappingsJson = NotificationDataGuard.Json(parameterMappingsJson, nameof(parameterMappingsJson));
	}

	public void RecordProviderStatus(string providerTemplateId, NotificationProviderTemplateState status, DateTimeOffset observedAt)
	{
		var id = NotificationDataGuard.Text(providerTemplateId, nameof(providerTemplateId), 256);
		NotificationRuntimeGuard.State(Enum.IsDefined(status) && status != NotificationProviderTemplateState.Draft &&
			(ProviderTemplateId == null || ProviderTemplateId == id));
		if (observedAt <= LastSyncedAt) return;
		ProviderTemplateId = id;
		ProviderStatus = status;
		LastSyncedAt = observedAt.ToUniversalTime();
	}
}
