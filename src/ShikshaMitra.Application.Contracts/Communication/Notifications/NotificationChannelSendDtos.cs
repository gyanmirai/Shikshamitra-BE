using System;

namespace ShikshaMitra.Communication.Notifications;

public enum NotificationProviderAcceptance { Accepted, Rejected, UnknownAcceptance }

public sealed record NotificationChannelSendRequest(
	Guid DispatchId, string IdempotencyKey, Guid ProviderSettingId,
	CommunicationChannelType Channel, string Destination, string? Subject, string Body,
	string? LinkUrl, string LanguageCode, int AttemptNumber,
	Guid? WhatsAppTemplateBindingId = null);

public sealed record NotificationChannelSendResult(
	NotificationProviderAcceptance Acceptance, string? ProviderMessageId,
	string? ErrorCode, bool IsTransient = false, TimeSpan? RetryAfter = null);
