using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Persists a single late-fee amount collected against a due line during a fee
/// collection transaction — mirrors FeeCollectionDueFeeAllocation's shape exactly,
/// but is kept as a separate table rather than a Type discriminator on the existing
/// one, since late fee is income booked distinctly from the fee itself (considerAsIncome
/// on CollectionPolicy) and doesn't reduce DueFee.OutstandingAmount the way a fee
/// payment does.
///
/// Known gap (collection-policy-implementation-plan.md Phase 3): reconciliation
/// reversal (cheque bounce) does not yet reverse these rows — only FeeCollectionDueFeeAllocation
/// is reversed today. A bounced cheque that included a late-fee amount will leave that
/// portion un-reversed until the reconciliation module is extended to know about this table.
/// </summary>
public class LateFeeCollectionAllocation : CreationAuditedEntity<Guid>, IMultiTenant
{
	private LateFeeCollectionAllocation() { /* For ORM */ }

	public LateFeeCollectionAllocation(
		Guid id,
		Guid? tenantId,
		Guid feeRecordMetadataId,
		Guid dueFeeId,
		double amount) : base(id)
	{
		TenantId = tenantId;
		FeeRecordMetadataId = feeRecordMetadataId;
		DueFeeId = dueFeeId;
		Amount = amount;
	}

	public Guid? TenantId { get; private set; }

	/// <summary>FK to the parent collection transaction.</summary>
	public Guid FeeRecordMetadataId { get; private set; }

	/// <summary>FK to the due line the late fee was computed against.</summary>
	public Guid DueFeeId { get; private set; }

	/// <summary>Late fee amount actually collected in this transaction for this due line.</summary>
	public double Amount { get; private set; }
}
