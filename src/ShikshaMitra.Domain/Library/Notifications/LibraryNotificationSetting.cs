using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationSetting : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string EventType { get; set; } = string.Empty;
	public string EventGroup { get; set; } = string.Empty;
	public string EventName { get; set; } = string.Empty;
	public string RecipientRole { get; set; } = string.Empty;
	public string Channel { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public bool IsChannelAvailable { get; set; }
	public bool IsComingSoon { get; set; }
	public int? ReminderOffsetDays { get; set; }
	public string SubjectTemplate { get; set; } = string.Empty;
	public string MessageTemplate { get; set; } = string.Empty;

	protected LibraryNotificationSetting()
	{
	}

	public LibraryNotificationSetting(
		Guid id,
		Guid? tenantId,
		string eventType,
		string eventGroup,
		string eventName,
		string recipientRole,
		string channel,
		bool isActive,
		bool isChannelAvailable,
		bool isComingSoon,
		int? reminderOffsetDays,
		string subjectTemplate,
		string messageTemplate)
		: base(id)
	{
		TenantId = tenantId;
		EventType = eventType;
		EventGroup = eventGroup;
		EventName = eventName;
		RecipientRole = recipientRole;
		Channel = channel;
		IsActive = isActive;
		IsChannelAvailable = isChannelAvailable;
		IsComingSoon = isComingSoon;
		ReminderOffsetDays = reminderOffsetDays;
		SubjectTemplate = subjectTemplate;
		MessageTemplate = messageTemplate;
	}
}
