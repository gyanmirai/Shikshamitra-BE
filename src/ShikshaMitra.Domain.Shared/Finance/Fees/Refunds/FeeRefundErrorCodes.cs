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
}
