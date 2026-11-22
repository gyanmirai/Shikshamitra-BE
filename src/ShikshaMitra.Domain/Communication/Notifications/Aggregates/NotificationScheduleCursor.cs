using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationScheduleCursor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string SchedulerKey { get; private set; } = string.Empty;
	public string CursorJson { get; private set; } = "{}";
	public DateTimeOffset NextScanAt { get; private set; }
	public Guid? LeaseId { get; private set; }
	public DateTimeOffset? LeaseExpiresAt { get; private set; }
	protected NotificationScheduleCursor() { }

	public NotificationScheduleCursor(Guid id, Guid tenantId, string schedulerKey, DateTimeOffset nextScanAt) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		SchedulerKey = NotificationDataGuard.Text(schedulerKey, nameof(schedulerKey), 192);
		NextScanAt = nextScanAt.ToUniversalTime();
	}

	public void Advance(string cursorJson, DateTimeOffset nextScanAt)
	{
		NotificationRuntimeGuard.Snapshot(cursorJson);
		Defer(nextScanAt);
		CursorJson = cursorJson;
	}

	public void Defer(DateTimeOffset nextScanAt)
	{
		NotificationRuntimeGuard.State(nextScanAt > NextScanAt);
		NextScanAt = nextScanAt.ToUniversalTime();
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
}
