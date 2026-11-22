using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationProviderEvent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ProviderSettingId { get; private set; }
	public string ProviderEventKey { get; private set; } = string.Empty;
	public string? ProviderMessageId { get; private set; }
	public string ProtectedPayload { get; private set; } = string.Empty;
	public DateTimeOffset ReceivedAt { get; private set; }
	public DateTimeOffset? ProcessedAt { get; private set; }
	protected NotificationProviderEvent() { }

	public NotificationProviderEvent(Guid id, Guid tenantId, Guid providerSettingId, string providerEventKey,
		string protectedPayload, DateTimeOffset receivedAt, string? providerMessageId = null) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		ProviderSettingId = NotificationDataGuard.Identifier(providerSettingId, nameof(providerSettingId));
		ProviderEventKey = NotificationDataGuard.Text(providerEventKey, nameof(providerEventKey), 192);
		ProtectedPayload = NotificationDataGuard.Text(protectedPayload, nameof(protectedPayload), 131072);
		ProviderMessageId = providerMessageId == null ? null : NotificationDataGuard.Text(providerMessageId, nameof(providerMessageId), 256);
		ReceivedAt = receivedAt.ToUniversalTime();
	}

	public void MarkProcessed(DateTimeOffset now) => ProcessedAt ??= now.ToUniversalTime();
}
