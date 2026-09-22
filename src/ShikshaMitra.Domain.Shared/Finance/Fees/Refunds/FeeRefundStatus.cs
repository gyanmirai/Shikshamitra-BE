namespace ShikshaMitra.Finance.Fees.FeeRefund;

public enum FeeRefundStatus
{
    Submitted = 0,
    Pending = Submitted,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Draft = 5,
    Processing = 6,
    Processed = 7,
    Failed = 8,
    Reversed = 9,
}

public enum FeeRefundReservationStatus
{
    Active = 0,
    Consumed = 1,
    Released = 2,
}

public enum FeeRefundSettlementStatus
{
    Processing = 0,
    Processed = 1,
    Failed = 2,
    Reversed = 3,
}
