using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationHistory : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string EventType { get; set; } = string.Empty;
	public string EventGroup { get; set; } = string.Empty;
	public string EventName { get; set; } = string.Empty;
	public string Channel { get; set; } = string.Empty;
	public string RecipientRole { get; set; } = string.Empty;
	public Guid? RecipientMemberId { get; set; }
	public string? RecipientName { get; set; }
	public string? RecipientEmail { get; set; }
	public string Subject { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? ErrorMessage { get; set; }
	public string? RelatedEntityType { get; set; }
	public Guid? RelatedEntityId { get; set; }
	public string? DeduplicationKey { get; set; }
	public DateTime? ReminderDate { get; set; }
	public DateTime? SentAt { get; set; }

	protected LibraryNotificationHistory()
	{
	}

	public LibraryNotificationHistory(
		Guid id,
		Guid? tenantId,
		string eventType,
		string eventGroup,
		string eventName,
		string channel,
		string recipientRole,
		Guid? recipientMemberId,
		string? recipientName,
		string? recipientEmail,
		string subject,
		string message,
		string status,
		string? errorMessage,
		string? relatedEntityType,
		Guid? relatedEntityId,
		string? deduplicationKey,
		DateTime? reminderDate,
		DateTime? sentAt)
		: base(id)
	{
		TenantId = tenantId;
		EventType = eventType;
		EventGroup = eventGroup;
		EventName = eventName;
		Channel = channel;
		RecipientRole = recipientRole;
		RecipientMemberId = recipientMemberId;
		RecipientName = recipientName;
		RecipientEmail = recipientEmail;
		Subject = subject;
		Message = message;
		Status = status;
		ErrorMessage = errorMessage;
		RelatedEntityType = relatedEntityType;
		RelatedEntityId = relatedEntityId;
		DeduplicationKey = deduplicationKey;
		ReminderDate = reminderDate;
		SentAt = sentAt;
	}
}
