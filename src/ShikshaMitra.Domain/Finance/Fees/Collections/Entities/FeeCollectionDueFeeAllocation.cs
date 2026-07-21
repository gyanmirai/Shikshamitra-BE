using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
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

    // ── Installment snapshot fields (new) ─────────────────────────────────────
    // These are denormalized from DueFee at allocation time so that receipt
    // generation does not need to re-query DueFee for display data.
    // If the master Payment Term is later renamed/changed, receipts retain
    // their original snapshot.

    /// <summary>Fee head name at allocation time (for receipt display).</summary>
    public string? FeeHeadName { get; private set; }

    /// <summary>Installment label at allocation time, e.g. "Apr-May".</summary>
    public string? InstallmentLabel { get; private set; }

    /// <summary>Coverage start date at allocation time.</summary>
    public DateTime? CoverageStartDate { get; private set; }

    /// <summary>Coverage end date at allocation time.</summary>
    public DateTime? CoverageEndDate { get; private set; }

    /// <summary>Due date at allocation time.</summary>
    public DateTime? DueDate { get; private set; }

    /// <summary>Schedule source at allocation time.</summary>
    public ScheduleSource ScheduleSource { get; private set; }

    /// <summary>
    /// Populates the installment snapshot fields from the associated DueFee.
    /// Called by the collection manager when creating the allocation.
    /// </summary>
    public void SetScheduleSnapshot(
        string? feeHeadName,
        string? installmentLabel,
        DateTime? coverageStartDate,
        DateTime? coverageEndDate,
        DateTime? dueDate,
        ScheduleSource scheduleSource)
    {
        FeeHeadName = feeHeadName;
        InstallmentLabel = installmentLabel;
        CoverageStartDate = coverageStartDate?.Date;
        CoverageEndDate = coverageEndDate?.Date;
        DueDate = dueDate?.Date;
        ScheduleSource = scheduleSource;
    }
}