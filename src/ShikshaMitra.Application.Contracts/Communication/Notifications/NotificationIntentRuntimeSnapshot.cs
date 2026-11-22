using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationIntentRuntimeSnapshot(int SchemaVersion, NotificationSourceScopeDto SourceScope,
	NotificationRuleRuntimeConfiguration Configuration, DateTimeOffset? DueOccurrenceAt = null, string? ReviewedDeliveryDigest = null)
{
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public NotificationDuePhase? DuePhase { get; init; }

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<string>? SelectedFamilyRelationships { get; init; }
}
