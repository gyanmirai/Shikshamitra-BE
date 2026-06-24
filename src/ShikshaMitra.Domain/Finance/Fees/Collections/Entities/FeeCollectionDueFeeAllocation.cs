using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Persists a single DueFee payment allocation made during a fee collection transaction.
/// Stored so that the reconciliation module can precisely reverse each allocation
/// when a payment instrument (e.g. cheque) is rejected.
/// </summary>
public class FeeCollectionDueFeeAllocation : CreationAuditedEntity<Guid>, IMultiTenant
{
    private FeeCollectionDueFeeAllocation() { /* For ORM */ }

    public FeeCollectionDueFeeAllocation(
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

    /// <summary>FK to the monthly fee obligation that was partially/fully settled.</summary>
    public Guid DueFeeId { get; private set; }

    /// <summary>Amount allocated from this transaction to the above DueFee.</summary>
    public double Amount { get; private set; }
}
