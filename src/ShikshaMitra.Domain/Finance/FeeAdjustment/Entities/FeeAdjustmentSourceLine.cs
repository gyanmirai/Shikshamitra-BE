using System;
using ShikshaMitra.Finance.Collections;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.FeeAdjustment.Entities;

public class FeeAdjustmentSourceLine : Entity<Guid>
{
    public Guid FeeAdjustmentRequestId { get; private set; }
    public Guid DueFeeId { get; private set; }
    public Guid FeeHeadId { get; private set; }
    public MonthEnum Month { get; private set; }
    public int Year { get; private set; }
    public double Amount { get; private set; }

    private FeeAdjustmentSourceLine() { }

    internal FeeAdjustmentSourceLine(
        Guid id,
        Guid feeAdjustmentRequestId,
        Guid dueFeeId,
        Guid feeHeadId,
        MonthEnum month,
        int year,
        double amount)
        : base(id)
    {
        FeeAdjustmentRequestId = feeAdjustmentRequestId;
        DueFeeId = dueFeeId;
        FeeHeadId = feeHeadId;
        Month = month;
        Year = year;
        Amount = amount;
    }
}
