using System;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationIntentRuntimeSnapshot(int SchemaVersion, NotificationSourceScopeDto SourceScope,
	NotificationRuleRuntimeConfiguration Configuration, DateTimeOffset? DueOccurrenceAt = null, string? ReviewedDeliveryDigest = null)
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public NotificationDuePhase? DuePhase { get; init; }
}
