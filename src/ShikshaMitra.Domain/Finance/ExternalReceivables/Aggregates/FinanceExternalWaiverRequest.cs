using System;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalWaiverRequest : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SourceLinkId { get; private set; }
	public Guid PolicyVersionId { get; private set; }
	public decimal Amount { get; private set; }
	public string Currency { get; private set; } = string.Empty;
	public bool RequiresApproval { get; private set; }
	public FinanceExternalWaiverState State { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public Guid? DecidedByUserId { get; private set; }
	public DateTimeOffset? DecidedAt { get; private set; }
	public string? DecisionReason { get; private set; }
	public Guid? AppliedWaiverId { get; private set; }
	private FinanceExternalWaiverRequest() { }

	public FinanceExternalWaiverRequest(Guid id, Guid tenantId, Guid sourceLinkId, Guid policyVersionId,
		decimal amount, string currency, bool requiresApproval, Guid actorId, string reason) : base(id)
	{
		FinanceExternalSettlementAmounts.EnsureMoney(amount, positive: true);
		if (id == Guid.Empty || tenantId == Guid.Empty || sourceLinkId == Guid.Empty || policyVersionId == Guid.Empty ||
			actorId == Guid.Empty || currency != "INR") throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		TenantId = tenantId; SourceLinkId = sourceLinkId; PolicyVersionId = policyVersionId;
		Amount = amount; Currency = currency; RequiresApproval = requiresApproval;
		RequestedByUserId = actorId; Reason = CheckedReason(reason); State = FinanceExternalWaiverState.PendingApproval;
	}

	public void Apply(Guid applicationId, Guid actorId, DateTimeOffset at, string reason)
	{
		EnsurePending();
		if (applicationId == Guid.Empty || actorId == Guid.Empty || at == default || RequiresApproval && actorId == RequestedByUserId)
			throw new BusinessException(LibraryFinanceErrorCodes.IndependentApprovalRequired);
		var decision = CheckedReason(reason);
		AppliedWaiverId = applicationId; DecidedByUserId = actorId; DecidedAt = at;
		DecisionReason = decision; State = FinanceExternalWaiverState.Applied;
	}

	public void Reject(Guid actorId, DateTimeOffset at, string reason)
	{
		EnsurePending();
		if (!RequiresApproval || actorId == Guid.Empty || actorId == RequestedByUserId || at == default)
			throw new BusinessException(LibraryFinanceErrorCodes.IndependentApprovalRequired);
		var decision = CheckedReason(reason);
		DecidedByUserId = actorId; DecidedAt = at; DecisionReason = decision; State = FinanceExternalWaiverState.Rejected;
	}

	private void EnsurePending()
	{
		if (State != FinanceExternalWaiverState.PendingApproval || AppliedWaiverId.HasValue)
			throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);
	}
	private static string CheckedReason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 1000
		? reason.Trim() : throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "A recorded decision reason is required.");
}
