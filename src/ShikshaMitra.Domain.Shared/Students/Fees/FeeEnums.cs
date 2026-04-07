namespace ShikshaMitra.Students.Fees;

/// <summary>
/// Enums for the Student Fees sub-domain.
/// Domain analogue of HR Payroll enums.
/// </summary>

/// <summary>
/// Represents the payment status of a student fee record.
/// </summary>
public enum FeePaymentStatus
{
    Pending    = 0,
    PartiallyPaid = 1,
    Paid       = 2,
    Overdue    = 3,
    Waived     = 4,
}

/// <summary>
/// Represents the lock state of a fee record for an academic period.
/// </summary>
public enum FeeRecordLockState
{
    Open   = 0,
    Locked = 1,
}
