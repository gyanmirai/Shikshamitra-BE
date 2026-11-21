using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationSettingsDto
{
	public bool CanManageSettings { get; set; }
	public LibraryNotificationSetupDto Setup { get; set; } = new();
	public List<LibraryNotificationSettingDto> Items { get; set; } = new();
	public List<LibraryNotificationEventOptionDto> Events { get; set; } = new();
	public List<LibraryNotificationChannelOptionDto> Channels { get; set; } = new();
}

public class LibraryNotificationSettingDto
{
	public Guid Id { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string EventGroup { get; set; } = string.Empty;
	public string EventName { get; set; } = string.Empty;
	public string RecipientRole { get; set; } = string.Empty;
	public string Channel { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public bool IsChannelAvailable { get; set; }
	public bool IsComingSoon { get; set; }
	public int? ReminderOffsetDays { get; set; }
	public int RepeatIntervalDays { get; set; } = 1;
	public string SubjectTemplate { get; set; } = string.Empty;
	public string MessageTemplate { get; set; } = string.Empty;
	public string? LastStatus { get; set; }
	public string? LastError { get; set; }
	public DateTime? LastSentAt { get; set; }
}

public class LibraryNotificationEventOptionDto
{
	public string EventType { get; set; } = string.Empty;
	public string EventGroup { get; set; } = string.Empty;
	public string EventName { get; set; } = string.Empty;
	public string RecipientRole { get; set; } = string.Empty;
	public bool IsComingSoon { get; set; }
}

public class LibraryNotificationChannelOptionDto
{
	public string Channel { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public bool IsAvailable { get; set; }
}

public class UpdateLibraryNotificationSettingsRequest
{
	public string? AutomaticReminderTimeLocal { get; set; }
	public string? StudentInboxRouting { get; set; }
	public List<Guid>? OperationalRecipientUserIds { get; set; }
	public List<LibraryNotificationSettingUpdateDto> Items { get; set; } = new();
}

public class LibraryNotificationSettingUpdateDto
{
	public Guid? Id { get; set; }
	public string? EventType { get; set; }
	public string? RecipientRole { get; set; }
	public string? Channel { get; set; }
	public bool? IsActive { get; set; }
	public int? ReminderOffsetDays { get; set; }
	public int? RepeatIntervalDays { get; set; }
	public string? SubjectTemplate { get; set; }
	public string? MessageTemplate { get; set; }
}

public class LibraryNotificationHistoryRequest
{
	public string? EventType { get; set; }
	public string? EventGroup { get; set; }
	public string? Channel { get; set; }
	public string? Status { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}

public class LibraryNotificationHistoryDto
{
	public string? RecipientKind { get; set; }
	public string? RecipientRelationship { get; set; }
	public Guid? RecipientUserId { get; set; }
	public DateTime? ReadAt { get; set; }
	public Guid Id { get; set; }
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
	public DateTime? ReminderDate { get; set; }
	public DateTime? SentAt { get; set; }
	public int AttemptCount { get; set; }
	public DateTime? NextAttemptAt { get; set; }
	public DateTime CreationTime { get; set; }
}

public class LibraryNotificationSetupDto
{
	public string? TimeZoneId { get; set; }
	public string? AutomaticReminderTimeLocal { get; set; }
	public string StudentInboxRouting { get; set; } = LibraryStudentInboxRouting.Guardians;
	public List<Guid> OperationalRecipientUserIds { get; set; } = new();
	public List<LibraryConfiguredOperationalRecipientDto> ConfiguredOperationalRecipients { get; set; } = new();
	public bool NeedsConfiguration { get; set; }
	public List<string> ConfigurationIssues { get; set; } = new();
}

public class LibraryOperationalRecipientOptionDto
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
}

public class LibraryConfiguredOperationalRecipientDto
{
	public Guid UserId { get; set; }
	public string? UserName { get; set; }
	public string? DisplayName { get; set; }
	public bool IsEligible { get; set; }
	public string? UnavailableReason { get; set; }
}

public class SendLibraryNotificationRequest
{
	public string EventType { get; set; } = LibraryNotificationEventTypes.DueDateReminder;
	public string Channel { get; set; } = LibraryNotificationChannels.InApp;
	public string? Subject { get; set; }
	public string Message { get; set; } = string.Empty;
	public string? RelatedEntityType { get; set; }
	public Guid? RelatedEntityId { get; set; }
	public List<LibraryNotificationRecipientRequest> Recipients { get; set; } = new();
}

public class LibraryNotificationRecipientRequest
{
	public Guid? MemberId { get; set; }
	public string? RecipientRole { get; set; }
	public string? RecipientName { get; set; }
	public string? RecipientEmail { get; set; }
}

public class LibraryNotificationSendResultDto
{
	public int Attempted { get; set; }
	public int Queued { get; set; }
	public int Sent { get; set; }
	public int Skipped { get; set; }
	public int Failed { get; set; }
	public List<LibraryNotificationHistoryDto> Items { get; set; } = new();
}

public class RetryLibraryNotificationRequest
{
	public bool AcknowledgePossibleDuplicate { get; set; }
}

public class LibraryNotificationRunRemindersRequest
{
	public DateTime? RunDate { get; set; }
}

public class LibraryNotificationRunRemindersResultDto : LibraryNotificationSendResultDto
{
	public DateTime RunDate { get; set; }
}
