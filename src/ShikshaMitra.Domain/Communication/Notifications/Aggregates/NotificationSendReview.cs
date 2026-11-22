using System;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing]
public class NotificationSendReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const int MaximumProtectedRequestLength = 4_000_000;
	public Guid? TenantId { get; private set; }
	public Guid ActorUserId { get; private set; }
	public string Digest { get; private set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; private set; }
	public DateTimeOffset? ConsumedAt { get; private set; }
	public Guid? OperationId { get; private set; }
	public string? IdempotencyKey { get; private set; }
	public string? ProtectedRequest { get; private set; }
	public Guid? AssignedApproverUserId { get; private set; }
	protected NotificationSendReview() { }
	public NotificationSendReview(Guid id, Guid tenantId, Guid actorId, string digest, DateTimeOffset expiresAt) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		ActorUserId = NotificationDataGuard.Identifier(actorId, nameof(actorId));
		Digest = NotificationDataGuard.Text(digest, nameof(digest), 128);
		ExpiresAt = expiresAt.ToUniversalTime();
	}

	public void BindRequest(string protectedRequest)
	{
		NotificationRuntimeGuard.State(ProtectedRequest == null && !OperationId.HasValue);
		ProtectedRequest = NotificationDataGuard.Text(protectedRequest, nameof(protectedRequest), MaximumProtectedRequestLength);
	}

	public void AssignApprover(Guid approverId)
	{
		NotificationRuntimeGuard.State(!AssignedApproverUserId.HasValue && !OperationId.HasValue &&
			approverId != Guid.Empty && approverId != ActorUserId);
		AssignedApproverUserId = approverId;
	}

	public Guid Consume(Guid operationId, string idempotencyKey, DateTimeOffset now)
	{
		NotificationDataGuard.Identifier(operationId, nameof(operationId));
		NotificationDataGuard.Text(idempotencyKey, nameof(idempotencyKey), 192);
		if (OperationId.HasValue)
		{
			if (string.Equals(IdempotencyKey, idempotencyKey, StringComparison.Ordinal)) return OperationId.Value;
			throw new BusinessException(NotificationErrorCodes.ReviewConsumed);
		}
		if (now >= ExpiresAt) throw new BusinessException(NotificationErrorCodes.ReviewExpired);
		OperationId = operationId;
		IdempotencyKey = idempotencyKey;
		ConsumedAt = now.ToUniversalTime();
		return operationId;
	}
}
