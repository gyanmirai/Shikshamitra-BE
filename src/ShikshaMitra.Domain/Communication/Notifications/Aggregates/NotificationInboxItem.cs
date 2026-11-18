using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing]
public class NotificationInboxItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const int MaximumBodyLength = 4000;
	public const int MaximumLinkLength = 1000;
	public Guid? TenantId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid DispatchId { get; private set; }
	public Guid? StudentId { get; private set; }
	public string SourceType { get; private set; } = string.Empty;
	public Guid SourceId { get; private set; }
	public string ContextKey { get; private set; } = string.Empty;
	public string LogicalKey { get; private set; } = string.Empty;
	public string Title { get; private set; } = string.Empty;
	public string Body { get; private set; } = string.Empty;
	public string RelativeLink { get; private set; } = string.Empty;
	public DateTimeOffset? ReadAt { get; private set; }
	public DateTimeOffset? AcknowledgedAt { get; private set; }
	protected NotificationInboxItem() { }

	public void MarkRead(DateTimeOffset at) => ReadAt ??= at.ToUniversalTime();
	public void Acknowledge(DateTimeOffset at)
	{
		AcknowledgedAt ??= at.ToUniversalTime();
		MarkRead(AcknowledgedAt.Value);
	}

	public NotificationInboxItem(Guid id, Guid tenantId, Guid userId, Guid dispatchId, string sourceType,
		Guid sourceId, string contextKey, string logicalKey, string title, string body, string relativeLink,
		Guid? studentId = null) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		UserId = NotificationDataGuard.Identifier(userId, nameof(userId));
		DispatchId = NotificationDataGuard.Identifier(dispatchId, nameof(dispatchId));
		SourceType = NotificationDataGuard.Text(sourceType, nameof(sourceType), 80);
		SourceId = NotificationDataGuard.Identifier(sourceId, nameof(sourceId));
		ContextKey = NotificationDataGuard.Text(contextKey, nameof(contextKey), 192);
		LogicalKey = NotificationDataGuard.Text(logicalKey, nameof(logicalKey), 128);
		Title = NotificationDataGuard.Text(title, nameof(title), 256);
		Body = NotificationDataGuard.Text(body, nameof(body), MaximumBodyLength);
		RelativeLink = NotificationDataGuard.Text(relativeLink, nameof(relativeLink), MaximumLinkLength);
		StudentId = studentId;
	}
}
