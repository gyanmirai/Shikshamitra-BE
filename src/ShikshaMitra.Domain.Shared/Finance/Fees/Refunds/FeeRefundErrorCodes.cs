namespace ShikshaMitra.Finance.Fees.FeeRefund;

public static class FeeRefundErrorCodes
{
    private const string Prefix = "Finance:FeeRefund:";

    public const string RefundNotFound             = Prefix + "010001";
    public const string InsufficientRefundBalance  = Prefix + "010002";
    public const string IllegalStatusTransition    = Prefix + "010003";
    public const string DuplicateRefundRequest     = Prefix + "010004";
    public const string StudentNotFound            = Prefix + "010005";
    public const string AcademicYearNotFound       = Prefix + "010006";
    public const string RefundLineNotFound         = Prefix + "010007";
    public const string RefundAlreadyProcessed     = Prefix + "010008";

    /// <summary>Thrown when an update or delete is attempted on a non-Pending refund request.</summary>
    public const string CannotModifyNonPendingRefund = Prefix + "010009";
    public const string PolicyNotAssigned            = Prefix + "010010";
    public const string PolicyVersionInvalid         = Prefix + "010011";
    public const string FeeHeadNotEligible            = Prefix + "010012";
    public const string RequestDeadlineExpired        = Prefix + "010013";
    public const string PayoutMethodNotAllowed        = Prefix + "010014";
    public const string MakerCheckerViolation         = Prefix + "010015";
    public const string ReservationConflict           = Prefix + "010016";
    public const string SettlementNotFound            = Prefix + "010017";
    public const string SettlementAlreadyProcessed    = Prefix + "010018";
    public const string ReversalNotAllowed             = Prefix + "010019";
    public const string SourceReceiptRequired          = Prefix + "010020";
    public const string SourceReceiptInvalid           = Prefix + "010021";
    public const string IdempotencyConflict            = Prefix + "010022";
    public const string RefundExceedsPolicyAmount      = Prefix + "010023";
    public const string AssignmentMismatch             = Prefix + "010024";
    public const string PaymentNotSettled               = Prefix + "010025";
}
