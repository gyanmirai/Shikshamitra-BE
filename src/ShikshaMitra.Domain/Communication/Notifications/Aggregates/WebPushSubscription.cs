using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing]
public class WebPushSubscription : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid BrowserInstallationId { get; private set; }
	public string EndpointHash { get; private set; } = string.Empty;
	public string ProtectedSubscription { get; private set; } = string.Empty;
	public string VapidKeyVersion { get; private set; } = string.Empty;
	public bool IsEnabled { get; private set; }
	public DateTimeOffset LastActivityAt { get; private set; }
	protected WebPushSubscription() { }

	public void Disable() => IsEnabled = false;
	public void Rebind(Guid userId, Guid browserInstallationId, string protectedSubscription, string vapidKeyVersion, DateTimeOffset now)
	{
		UserId = NotificationDataGuard.Identifier(userId, nameof(userId));
		BrowserInstallationId = NotificationDataGuard.Identifier(browserInstallationId, nameof(browserInstallationId));
		ProtectedSubscription = NotificationDataGuard.Text(protectedSubscription, nameof(protectedSubscription), 16384);
		VapidKeyVersion = NotificationDataGuard.Text(vapidKeyVersion, nameof(vapidKeyVersion), 64);
		LastActivityAt = now.ToUniversalTime(); IsEnabled = true;
	}

	public WebPushSubscription(Guid id, Guid tenantId, Guid userId, Guid browserInstallationId,
		string endpointHash, string protectedSubscription, string vapidKeyVersion, DateTimeOffset now) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		UserId = NotificationDataGuard.Identifier(userId, nameof(userId));
		BrowserInstallationId = NotificationDataGuard.Identifier(browserInstallationId, nameof(browserInstallationId));
		EndpointHash = NotificationDataGuard.Text(endpointHash, nameof(endpointHash), 128);
		ProtectedSubscription = NotificationDataGuard.Text(protectedSubscription, nameof(protectedSubscription), 16384);
		VapidKeyVersion = NotificationDataGuard.Text(vapidKeyVersion, nameof(vapidKeyVersion), 64);
		IsEnabled = true;
		LastActivityAt = now.ToUniversalTime();
	}
}
