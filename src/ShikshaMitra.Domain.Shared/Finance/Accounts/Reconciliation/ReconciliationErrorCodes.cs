namespace ShikshaMitra.Finance.Accounts.Reconciliation;

/// <summary>
/// Centralized error codes for the Finance Reconciliation module.
/// Convention: "Finance:Reconciliation:06xxxx"
/// </summary>
public static class ReconciliationErrorCodes
{
    private const string Prefix = "Finance:Reconciliation:";

    // ── Receipt state guards ───────────────────────────────────────────────

    public const string ReceiptNotFound           = Prefix + "060001";
    public const string NotProvisionalFeeReceipt  = Prefix + "060002";
    public const string AlreadyVerified           = Prefix + "060003";
    public const string AlreadyRejected           = Prefix + "060004";
    public const string TerminalStatus            = Prefix + "060005";

    // ── Financial rollback ────────────────────────────────────────────────

    public const string AllocationNotFound        = Prefix + "060010";
    public const string ReverseExceedsPaidAmount  = Prefix + "060011";

    // ── Input validation ──────────────────────────────────────────────────

    public const string RemarksRequiredForReject  = Prefix + "060020";
    public const string InvalidPaymentMethod      = Prefix + "060021";
}
