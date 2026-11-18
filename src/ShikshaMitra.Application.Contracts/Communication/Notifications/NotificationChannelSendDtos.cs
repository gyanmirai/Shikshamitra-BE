using System;
using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

public enum NotificationProviderAcceptance { Accepted, Rejected, UnknownAcceptance }

public sealed record NotificationChannelSendRequest(
	Guid DispatchId, string IdempotencyKey, Guid ProviderSettingId,
	CommunicationChannelType Channel, string Destination, string? Subject, string Body,
	string? LinkUrl, string LanguageCode, int AttemptNumber,
	Guid? WhatsAppTemplateBindingId = null, Guid? TemplateRevisionId = null,
	NotificationRecipientRefDto? Recipient = null,
	IReadOnlyDictionary<string, NotificationVariableValueDto>? WhatsAppVariables = null)
{
	public override string ToString() => nameof(NotificationChannelSendRequest);
}

public sealed record NotificationChannelSendResult(
	NotificationProviderAcceptance Acceptance, string? ProviderMessageId,
	string? ErrorCode, bool IsTransient = false, TimeSpan? RetryAfter = null);
