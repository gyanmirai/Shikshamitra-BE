using System;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationQueueClaim(Guid Id, Guid TenantId, Guid LeaseId);
