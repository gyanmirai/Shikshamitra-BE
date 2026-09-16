using System;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

/// <summary>Durable proof that one logical concession command completed.</summary>
public class ConcessionCommandReceipt : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConcessionRequestId { get; private set; }
	public Guid ActorId { get; private set; }
	public string Action { get; private set; } = string.Empty;
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadHash { get; private set; } = string.Empty;

	private ConcessionCommandReceipt() { }

	public ConcessionCommandReceipt(
		Guid id,
		Guid? tenantId,
		Guid concessionRequestId,
		Guid actorId,
		string action,
		string idempotencyKey,
		string payloadHash)
	{
		if (concessionRequestId == Guid.Empty || actorId == Guid.Empty || string.IsNullOrWhiteSpace(action) ||
			string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 128 ||
			string.IsNullOrWhiteSpace(payloadHash) || payloadHash.Trim().Length != 64)
			throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);

		Id = id;
		TenantId = tenantId;
		ConcessionRequestId = concessionRequestId;
		ActorId = actorId;
		Action = action.Trim().ToLowerInvariant();
		IdempotencyKey = idempotencyKey.Trim();
		PayloadHash = payloadHash.Trim().ToUpperInvariant();
	}
}
