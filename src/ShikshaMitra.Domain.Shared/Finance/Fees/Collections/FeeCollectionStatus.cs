namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// Status for a fee collection transaction or student collection summary.
/// </summary>
public enum FeeCollectionStatus
{
    /// <summary>No payment made yet, or partially paid.</summary>
    Pending = 0,

    /// <summary>Fully paid.</summary>
    Paid = 1,

    /// <summary>Payment due date has passed without full payment.</summary>
    Overdue = 2,

    /// <summary>
    /// Payment instrument (e.g. cheque/DD) was rejected by the bank.
    /// DueFee allocations are reversed and the collection is treated as void.
    /// </summary>
    Reversed = 3,

    /// <summary>Some payment has been received, but outstanding balance remains.</summary>
    Partial = 4,

    /// <summary>The transaction needs bank/instrument clearance/verification.</summary>
    InReconciliation = 5,

    /// <summary>A charge or late-fee balance was cleared by a non-cash waiver.</summary>
    Waived = 6,

    /// <summary>Summary only: no fee structure assigned and no fee or charge amounts recorded.</summary>
    NoFeeAssigned = 7,
}
