using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public partial class NotificationIntent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? RuleId { get; private set; }
	public int RuleRevision { get; private set; }
	public string EventKey { get; private set; } = string.Empty;
	public string SourceType { get; private set; } = string.Empty;
	public Guid SourceId { get; private set; }
	public string SourceRevision { get; private set; } = string.Empty;
	public Guid? ActorUserId { get; private set; }
	public Guid? CampaignId { get; private set; }
	public string DeduplicationKey { get; private set; } = string.Empty;
	public string AffectedRecipientsJson { get; private set; } = "[]";
	public string TemplateRevisionIdsJson { get; private set; } = "[]";
	public DateTimeOffset OccurredAt { get; private set; }
	public DateTimeOffset DueAt { get; private set; }
	public DateTimeOffset? ExpiresAt { get; private set; }
	public NotificationIntentState Status { get; private set; }
	public Guid? LeaseId { get; private set; }
	public DateTimeOffset? ClaimedAt { get; private set; }
	public DateTimeOffset? LeaseExpiresAt { get; private set; }
	protected NotificationIntent() { }

	public NotificationIntent(Guid id, Guid tenantId, Guid ruleId, int ruleRevision, string eventKey,
		string sourceType, Guid sourceId, string sourceRevision, string deduplicationKey,
		DateTimeOffset occurredAt, DateTimeOffset dueAt, string affectedRecipientsJson,
		string templateRevisionIdsJson, DateTimeOffset? expiresAt = null, Guid? actorUserId = null)
		: this(id, tenantId, (Guid?)NotificationDataGuard.Identifier(ruleId, nameof(ruleId)), ruleRevision, eventKey, sourceType,
			sourceId, sourceRevision, deduplicationKey, occurredAt, dueAt, affectedRecipientsJson, templateRevisionIdsJson,
			expiresAt, actorUserId)
	{
	}

	private NotificationIntent(Guid id, Guid tenantId, Guid? ruleId, int ruleRevision, string eventKey,
		string sourceType, Guid sourceId, string sourceRevision, string deduplicationKey,
		DateTimeOffset occurredAt, DateTimeOffset dueAt, string affectedRecipientsJson,
		string templateRevisionIdsJson, DateTimeOffset? expiresAt, Guid? actorUserId) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		RuleId = ruleId.HasValue ? NotificationDataGuard.Identifier(ruleId.Value, nameof(ruleId)) : null;
		if (ruleId.HasValue ? ruleRevision < 1 : ruleRevision != 0) throw new ArgumentOutOfRangeException(nameof(ruleRevision));
		RuleRevision = ruleRevision;
		EventKey = NotificationDataGuard.Text(eventKey, nameof(eventKey), 160);
		SourceType = NotificationDataGuard.Text(sourceType, nameof(sourceType), 80);
		SourceId = NotificationDataGuard.Identifier(sourceId, nameof(sourceId));
		SourceRevision = NotificationDataGuard.Text(sourceRevision, nameof(sourceRevision), 128);
		DeduplicationKey = NotificationDataGuard.Text(deduplicationKey, nameof(deduplicationKey), 128);
		AffectedRecipientsJson = NotificationDataGuard.Json(affectedRecipientsJson, nameof(affectedRecipientsJson));
		TemplateRevisionIdsJson = NotificationDataGuard.Json(templateRevisionIdsJson, nameof(templateRevisionIdsJson));
		OccurredAt = occurredAt.ToUniversalTime();
		DueAt = dueAt.ToUniversalTime();
		ExpiresAt = expiresAt?.ToUniversalTime();
		ActorUserId = actorUserId;
		Status = DueAt > OccurredAt ? NotificationIntentState.Scheduled : NotificationIntentState.Ready;
	}

	public static NotificationIntent CreateReviewedManual(Guid id, Guid tenantId, Guid? ruleId, int ruleRevision,
		string eventKey, string sourceType, Guid sourceId, string sourceRevision, string deduplicationKey,
		DateTimeOffset now, string affectedRecipientsJson, string templateRevisionIdsJson,
		DateTimeOffset? expiresAt, Guid actorId, Guid operationId, string runtimeSnapshot)
	{
		var intent = new NotificationIntent(id, tenantId, ruleId, ruleRevision, eventKey, sourceType, sourceId,
			sourceRevision, deduplicationKey, now, now, affectedRecipientsJson, templateRevisionIdsJson, expiresAt, actorId);
		intent.BindRuntimeSnapshot(runtimeSnapshot, NotificationIntentOrigin.ReviewedManual, operationId);
		return intent;
	}
}
