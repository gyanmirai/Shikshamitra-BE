using System;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalSettlementPolicy : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SourceLinkId { get; private set; }
	public Guid CollectionPolicyVersionId { get; private set; }
	public Guid? RefundPolicyVersionId { get; private set; }
	public Guid BoundByUserId { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	private FinanceExternalSettlementPolicy() { }

	public FinanceExternalSettlementPolicy(Guid id, Guid tenantId, Guid sourceLinkId, Guid collectionVersionId,
		Guid? refundVersionId, Guid actorId, string reason) : base(id)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || sourceLinkId == Guid.Empty || collectionVersionId == Guid.Empty ||
			refundVersionId == Guid.Empty || actorId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		TenantId = tenantId; SourceLinkId = sourceLinkId; CollectionPolicyVersionId = collectionVersionId;
		RefundPolicyVersionId = refundVersionId; BoundByUserId = actorId; Reason = reason.Trim();
	}
}
