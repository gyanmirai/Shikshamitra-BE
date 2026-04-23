using ShikshaMitra.Finance.ConcessionRequests;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionFeeHeadMapping : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid ConcessionRequestId { get; private set; }

    public Guid FeeHeadId { get; private set; }

    /// <summary>
    /// Calculated concession amount applied to this fee head.
    /// </summary>
    public decimal FeeHeadConcessionAmount { get; private set; }

    /// <summary>
    /// Applicable fee head amount before concession.
    /// </summary>
    public decimal PreConcessionFeeHeadAmount { get; private set; }

    /// <summary>
    /// Applicable fee head amount after concession.
    /// </summary>
    public decimal PostConcessionFeeHeadAmount { get; private set; }

    /// <summary>
    /// User-specified concession value (either percentage or flat amount).
    /// </summary>
    public decimal UserSpecifiedConcessionAmountValue { get; private set; }

    public AmountType UserSpecifiedConcessionAmountType { get; private set; }

    protected ConcessionFeeHeadMapping()
    {
        /* Required by ORM / deserialization */
    }

    public ConcessionFeeHeadMapping(
        Guid id,
        Guid? tenantId,
        Guid concessionRequestId,
        Guid feeHeadId,
        decimal preConcessionFeeHeadAmount,
        decimal userSpecifiedConcessionAmountValue,
        AmountType userSpecifiedConcessionAmountType) : base(id)
    {
        TenantId = tenantId;
        ConcessionRequestId = Check.NotNull(concessionRequestId, nameof(concessionRequestId));
        FeeHeadId = Check.NotNull(feeHeadId, nameof(feeHeadId));

        if (preConcessionFeeHeadAmount < 0)
        {
            throw new ArgumentException("Pre-concession amount cannot be negative.", nameof(preConcessionFeeHeadAmount));
        }

        if (userSpecifiedConcessionAmountValue < 0)
        {
            throw new ArgumentException("Concession value cannot be negative.", nameof(userSpecifiedConcessionAmountValue));
        }

        PreConcessionFeeHeadAmount = preConcessionFeeHeadAmount;
        UserSpecifiedConcessionAmountValue = userSpecifiedConcessionAmountValue;
        UserSpecifiedConcessionAmountType = userSpecifiedConcessionAmountType;

        CalculateConcessionAmounts();
    }

    /// <summary>
    /// Recalculate derived amounts whenever input values change.
    /// </summary>
    private void CalculateConcessionAmounts()
    {
        if (UserSpecifiedConcessionAmountType == AmountType.Percentage)
        {
            FeeHeadConcessionAmount = Math.Round(PreConcessionFeeHeadAmount * UserSpecifiedConcessionAmountValue / 100, 2);
        }
        else
        {
            FeeHeadConcessionAmount = Math.Min(UserSpecifiedConcessionAmountValue, PreConcessionFeeHeadAmount);
        }

        PostConcessionFeeHeadAmount = PreConcessionFeeHeadAmount - FeeHeadConcessionAmount;
    }

    /// <summary>
    /// Updates the mapping values (only valid while the parent ConcessionRequest is in Draft state).
    /// </summary>
    internal void Update(
        decimal preConcessionFeeHeadAmount,
        decimal userSpecifiedConcessionAmountValue,
        AmountType userSpecifiedConcessionAmountType)
    {
        if (preConcessionFeeHeadAmount < 0)
        {
            throw new ArgumentException("Pre-concession amount cannot be negative.", nameof(preConcessionFeeHeadAmount));
        }

        if (userSpecifiedConcessionAmountValue < 0)
        {
            throw new ArgumentException("Concession value cannot be negative.", nameof(userSpecifiedConcessionAmountValue));
        }

        PreConcessionFeeHeadAmount = preConcessionFeeHeadAmount;
        UserSpecifiedConcessionAmountValue = userSpecifiedConcessionAmountValue;
        UserSpecifiedConcessionAmountType = userSpecifiedConcessionAmountType;

        CalculateConcessionAmounts();
    }
}

