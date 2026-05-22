namespace ShikshaMitra.Finance.FeeCollection;

/// <summary>
/// Payment status for a fee collection transaction.
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
}
