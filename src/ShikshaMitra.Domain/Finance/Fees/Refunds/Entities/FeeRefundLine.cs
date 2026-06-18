using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// An individual refund line within a <see cref="FeeRefundRequest"/>.
/// Points to a specific <see cref="FeeCollection.DueFee"/> row and records the
/// requested refund amount for that monthly obligation.
/// This is an owned entity — persisted in a dedicated table via OwnsMany.
/// </summary>
public class FeeRefundLine : Entity<Guid>
{
    /// <summary>Foreign key to the parent <see cref="FeeRefundRequest"/> (shadow property in EF).</summary>
    public Guid FeeRefundRequestId { get; private set; }

    /// <summary>The monthly DueFee row this line targets.</summary>
    public Guid DueFeeId { get; private set; }

    /// <summary>Amount requested for refund against the linked DueFee.</summary>
    public double Amount { get; private set; }

    private FeeRefundLine() { /* For ORM */ }

    internal FeeRefundLine(Guid id, Guid feeRefundRequestId, Guid dueFeeId, double amount)
        : base(id)
    {
        FeeRefundRequestId = feeRefundRequestId;
        DueFeeId = dueFeeId;
        Amount = amount;
    }
}
