using System;
using Volo.Abp;
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
	public Guid? RecipientUserId { get; private set; }
	public DateTime? ReadAt { get; private set; }
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
	public int AttemptCount { get; private set; }
	public DateTime? ClaimedAt { get; private set; }
	public DateTime? NextAttemptAt { get; private set; }

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

	public void QueueForDelivery(DateTime now, bool acknowledgeUnconfirmed = false)
	{
		if (Status == LibraryNotificationDeliveryStatuses.Unconfirmed && !acknowledgeUnconfirmed)
			throw new BusinessException(LibraryNotificationErrorCodes.DuplicateAcknowledgementRequired);
		if (Status is not (LibraryNotificationDeliveryStatuses.Queued or LibraryNotificationDeliveryStatuses.Failed or LibraryNotificationDeliveryStatuses.Unconfirmed))
			throw new BusinessException(LibraryNotificationErrorCodes.RetryNotAllowed);
		Status = LibraryNotificationDeliveryStatuses.Queued;
		NextAttemptAt = now;
		ClaimedAt = null;
		SentAt = null;
		ErrorMessage = null;
		ExtraProperties.Remove("DispatchClaimId");
	}

	public void BindRecipient(Guid? userId) => RecipientUserId = userId == Guid.Empty ? null : userId;

	public void DeliverToInbox(DateTime now)
	{
		if (Channel != LibraryNotificationChannels.InApp || !RecipientUserId.HasValue)
			throw new BusinessException(LibraryNotificationErrorCodes.RecipientAccountRequired);
		Status = LibraryNotificationDeliveryStatuses.Sent;
		SentAt = now;
		ErrorMessage = null;
	}

	public void MarkRead(Guid userId, DateTime now)
	{
		if (Channel != LibraryNotificationChannels.InApp || Status != LibraryNotificationDeliveryStatuses.Sent || RecipientUserId != userId)
			throw new BusinessException(LibraryNotificationErrorCodes.RecipientAccountRequired);
		ReadAt ??= now;
	}

	public bool IsDue(DateTime now) => Status == LibraryNotificationDeliveryStatuses.Queued && NextAttemptAt <= now;

	public void Claim(DateTime now, Guid claimId)
	{
		if (!IsDue(now) || claimId == Guid.Empty) throw new BusinessException(LibraryNotificationErrorCodes.RetryNotAllowed);
		Status = LibraryNotificationDeliveryStatuses.Processing;
		ClaimedAt = now;
		NextAttemptAt = null;
		AttemptCount++;
		ExtraProperties["DispatchClaimId"] = claimId.ToString("N");
	}

	public bool HasClaim(Guid claimId) => Status == LibraryNotificationDeliveryStatuses.Processing &&
		ExtraProperties.TryGetValue("DispatchClaimId", out var saved) && saved?.ToString() == claimId.ToString("N");

	public void MarkSent(DateTime now)
	{
		EnsureProcessing();
		Status = LibraryNotificationDeliveryStatuses.Sent;
		SentAt = now;
		FinishAttempt(null);
	}

	public void MarkSkipped(string reason)
	{
		Status = LibraryNotificationDeliveryStatuses.Skipped;
		SentAt = null;
		FinishAttempt(reason);
	}

	public void RecordFailure(DateTime now, bool acceptanceUnknown, bool transient)
	{
		EnsureProcessing();
		SentAt = null;
		Status = acceptanceUnknown ? LibraryNotificationDeliveryStatuses.Unconfirmed : LibraryNotificationDeliveryStatuses.Failed;
		FinishAttempt(acceptanceUnknown
			? "The email sender's acceptance could not be confirmed. Review delivery before retrying."
			: "The email sender rejected this notification. Review school email settings.");
		if (!acceptanceUnknown && transient && AttemptCount < 3)
		{
			Status = LibraryNotificationDeliveryStatuses.Queued;
			NextAttemptAt = now.AddMinutes(Math.Pow(2, AttemptCount));
		}
	}

	public void RecoverInterrupted(DateTime now)
	{
		if (Status == LibraryNotificationDeliveryStatuses.Processing && ClaimedAt <= now.AddMinutes(-10))
			RecordFailure(now, acceptanceUnknown: true, transient: false);
	}

	private void FinishAttempt(string? error)
	{
		ClaimedAt = null;
		NextAttemptAt = null;
		ErrorMessage = error;
		ExtraProperties.Remove("DispatchClaimId");
	}

	private void EnsureProcessing()
	{
		if (Status != LibraryNotificationDeliveryStatuses.Processing)
			throw new BusinessException(LibraryNotificationErrorCodes.RetryNotAllowed);
	}
}
