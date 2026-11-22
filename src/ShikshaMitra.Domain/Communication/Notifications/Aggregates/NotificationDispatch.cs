using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public partial class NotificationDispatch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid IntentId { get; private set; }
	public Guid TemplateRevisionId { get; private set; }
	public string RecipientReferenceJson { get; private set; } = "{}";
	public string RecipientContextKey { get; private set; } = string.Empty;
	public CommunicationChannelType Channel { get; private set; }
	public string DestinationFingerprint { get; private set; } = string.Empty;
	public string ProtectedMessageJson { get; private set; } = string.Empty;
	public string ContentHash { get; private set; } = string.Empty;
	public string DeduplicationKey { get; private set; } = string.Empty;
	public Guid? CampaignId { get; private set; }
	public Guid? RecipientSnapshotId { get; private set; }
	public Guid? ProviderSettingId { get; private set; }
	public Guid? DeliveryAttemptId { get; private set; }
	public NotificationDispatchState Status { get; private set; }
	public int AttemptNumber { get; private set; }
	public DateTimeOffset NextAttemptAt { get; private set; }
	public DateTimeOffset? ExpiresAt { get; private set; }
	public Guid? LeaseId { get; private set; }
	public DateTimeOffset? ClaimedAt { get; private set; }
	public DateTimeOffset? LeaseExpiresAt { get; private set; }
	public string? ProviderMessageId { get; private set; }
	public string? ErrorCode { get; private set; }
	protected NotificationDispatch() { }

	public NotificationDispatch(Guid id, Guid tenantId, Guid intentId, Guid templateRevisionId,
		string recipientReferenceJson, string recipientContextKey, CommunicationChannelType channel,
		string destinationFingerprint, string protectedMessageJson, string contentHash, string deduplicationKey,
		DateTimeOffset nextAttemptAt, DateTimeOffset? expiresAt = null) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		IntentId = NotificationDataGuard.Identifier(intentId, nameof(intentId));
		TemplateRevisionId = NotificationDataGuard.Identifier(templateRevisionId, nameof(templateRevisionId));
		RecipientReferenceJson = NotificationDataGuard.Json(recipientReferenceJson, nameof(recipientReferenceJson));
		RecipientContextKey = NotificationDataGuard.Text(recipientContextKey, nameof(recipientContextKey), 192);
		if (!Enum.IsDefined(channel) || channel == CommunicationChannelType.Sms)
			throw new ArgumentOutOfRangeException(nameof(channel));
		Channel = channel;
		DestinationFingerprint = NotificationDataGuard.Text(destinationFingerprint, nameof(destinationFingerprint), 128);
		ProtectedMessageJson = NotificationDataGuard.Text(protectedMessageJson, nameof(protectedMessageJson), 131072);
		ContentHash = NotificationDataGuard.Text(contentHash, nameof(contentHash), 128);
		DeduplicationKey = NotificationDataGuard.Text(deduplicationKey, nameof(deduplicationKey), 128);
		NextAttemptAt = nextAttemptAt.ToUniversalTime();
		ExpiresAt = expiresAt?.ToUniversalTime();
	}
}
