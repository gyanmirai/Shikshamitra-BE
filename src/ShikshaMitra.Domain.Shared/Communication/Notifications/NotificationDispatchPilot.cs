using System;

namespace ShikshaMitra.Communication.Notifications;

public abstract record NotificationDispatchPilot(Guid TenantId, Guid OperationId, Guid RecipientUserId);
