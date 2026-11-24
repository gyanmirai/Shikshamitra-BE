using System;

namespace ShikshaMitra.Communication.Notifications;

public sealed record StagingInAppNotificationPilot(Guid TenantId, Guid OperationId, Guid RecipientUserId, Guid SourceId)
	: NotificationDispatchPilot(TenantId, OperationId, RecipientUserId);
