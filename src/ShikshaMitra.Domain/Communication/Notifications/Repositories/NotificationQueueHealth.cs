using System;
using System.Collections.Generic;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationChannelQueueHealth(CommunicationChannelType Channel, long Pending, long Due,
	DateTimeOffset? OldestDueAt, long Processing, long UnknownAcceptance, long RecentFailures,
	long PendingRetries, long PendingThrottled, long RecentExpiredSubscriptions,
	long RecentConsentExclusions, long RecentSetupExclusions);

public sealed record NotificationQueueHealth(long DueIntents, DateTimeOffset? OldestDueIntentAt,
	IReadOnlyList<NotificationChannelQueueHealth> Channels, long PendingWhatsAppReceipts, DateTimeOffset? OldestWhatsAppReceiptAt);
