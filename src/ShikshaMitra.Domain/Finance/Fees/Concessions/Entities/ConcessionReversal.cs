using System;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

/// <summary>
/// Append-only journal entry for a concession credit reversal. Entries are never updated
/// or deleted; the request status is derived from the sum of these entries.
/// </summary>
public class ConcessionReversal : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ConcessionRequestId { get; private set; }
	public Guid ConcessionDueFeeAllocationId { get; private set; }
	public Guid DueFeeId { get; private set; }
	public Guid FeeHeadId { get; private set; }
	public decimal Amount { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public Guid ReversedBy { get; private set; }
	public DateTime ReversedAt { get; private set; }

	private ConcessionReversal() { }

	public ConcessionReversal(
		Guid id,
		Guid? tenantId,
		Guid concessionRequestId,
		Guid concessionDueFeeAllocationId,
		Guid dueFeeId,
		Guid feeHeadId,
		decimal amount,
		string reason,
		Guid reversedBy,
		DateTime reversedAt)
	{
		if (amount <= 0m || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal);

		Id = id;
		TenantId = tenantId;
		ConcessionRequestId = concessionRequestId;
		ConcessionDueFeeAllocationId = concessionDueFeeAllocationId;
		DueFeeId = dueFeeId;
		FeeHeadId = feeHeadId;
		Amount = amount;
		Reason = reason.Trim();
		ReversedBy = reversedBy;
		ReversedAt = reversedAt;
	}
}
