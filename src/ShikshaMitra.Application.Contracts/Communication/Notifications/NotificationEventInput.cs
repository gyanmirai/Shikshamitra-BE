using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Communication.Notifications;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotificationRecipientRefDto(string Kind, Guid? StudentId = null,
	string? FamilyRelationship = null, Guid? UserId = null, Guid? EmployeeId = null);

public sealed record NotificationEventInput(
	string EventKey, string SourceType, Guid SourceId, string SourceRevision,
	DateTimeOffset OccurredAt, IReadOnlyList<NotificationRecipientRefDto> AffectedRecipients,
	Guid? ActorUserId = null, DateTimeOffset? DueAt = null, DateTimeOffset? ExpiresAt = null,
	NotificationDueOccurrenceDto? DueOccurrence = null);

public enum NotificationDuePhase
{
	ReviewerEscalation = 1
}

public sealed record NotificationDueOccurrenceDto(NotificationSourceScopeDto Scope, DateTimeOffset OccurrenceAt,
	Guid RuleId, int RuleRevision)
{
	public NotificationDuePhase? Phase { get; init; }
}

public sealed record NotificationIntentContext(
	Guid IntentId, Guid TenantId, string EventKey, string SourceType, Guid SourceId,
	string SourceRevision, DateTimeOffset OccurredAt,
	IReadOnlyList<NotificationRecipientRefDto> AffectedRecipients,
	NotificationSourceScopeDto? SourceScope = null, DateTimeOffset? DueOccurrenceAt = null)
{
	public NotificationDuePhase? DuePhase { get; init; }
}

public sealed record NotificationVariableValueDto(NotificationVariableType Type, string? Value);

public sealed record NotificationSourceScopeDto(Guid? AcademicYearId = null, Guid? BoardId = null,
	Guid? MediumId = null, Guid? ClassId = null, Guid? DivisionId = null);

public sealed record NotificationSourceAudienceSliceDto(NotificationSourceScopeDto Scope,
	IReadOnlyList<NotificationRecipientRefDto> Recipients);

/// <summary>Replaces the recipient's complete source content; never merge it with shared variables.</summary>
public sealed record NotificationSourceRecipientContentDto(NotificationRecipientRefDto Recipient,
	IReadOnlyDictionary<string, NotificationVariableValueDto> Variables, string? RelativeLink);

public sealed record NotificationSourceSnapshot(
	bool IsEligible, string? ExclusionCode, string SourceRevision, string? RelativeLink,
	IReadOnlyDictionary<string, NotificationVariableValueDto> Variables,
	IReadOnlyList<NotificationRecipientRefDto> EligibleRecipients,
	DateTimeOffset? ExpiresAt = null,
	IReadOnlyList<NotificationSourceAudienceSliceDto>? AudienceSlices = null,
	IReadOnlyList<NotificationSourceRecipientContentDto>? RecipientContent = null);
