using System;
using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationRenderInput(
	Guid RevisionId, string EventKey, int SchemaVersion, string RecipientKind,
	CommunicationChannelType Channel, string LanguageCode,
	IReadOnlyDictionary<string, NotificationVariableValueDto> Variables);

public sealed record NotificationRenderResult(
	bool IsSuccess, string? ErrorCode, string? Subject, string? Body,
	string? LinkUrl, string? ContentHash);
