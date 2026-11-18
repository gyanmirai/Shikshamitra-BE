using System;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationIntentRuntimeSnapshot(int SchemaVersion, NotificationSourceScopeDto SourceScope,
	NotificationRuleRuntimeConfiguration Configuration, DateTimeOffset? DueOccurrenceAt = null, string? ReviewedDeliveryDigest = null);
