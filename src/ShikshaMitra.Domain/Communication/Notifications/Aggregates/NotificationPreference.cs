using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationPreference : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid? StudentId { get; private set; }
	public string ContextKey { get; private set; } = string.Empty;
	public string Category { get; private set; } = string.Empty;
	public string ConfigurationJson { get; private set; } = "{}";
	public string LanguageCode { get; private set; } = "en";
	protected NotificationPreference() { }

	public NotificationPreference(Guid id, Guid tenantId, Guid userId, string category, Guid? studentId = null) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		UserId = NotificationDataGuard.Identifier(userId, nameof(userId));
		Category = NotificationDataGuard.Text(category, nameof(category), 80);
		if (studentId.HasValue) NotificationDataGuard.Identifier(studentId.Value, nameof(studentId));
		StudentId = studentId;
		ContextKey = studentId.HasValue ? $"student:{studentId.Value:N}" : "user";
	}
}
