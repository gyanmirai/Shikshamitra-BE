namespace ShikshaMitra.Finance.FeeCollection;

public static class FeeCollectionErrorCodes
{
    private const string Prefix = "ShikshaMitra:FeeCollection:";

    public const string PaymentAmountMustBePositive = Prefix + "PaymentAmountMustBePositive";
    public const string PaymentDetailsRequiredForNonCash = Prefix + "PaymentDetailsRequiredForNonCash";
    public const string PaymentDetailsMismatch = Prefix + "PaymentDetailsMismatch";
    public const string InvalidFeeHead = Prefix + "InvalidFeeHead";
    public const string InvalidCharge = Prefix + "InvalidCharge";
    public const string AmountExceedsOutstanding = Prefix + "AmountExceedsOutstanding";
    public const string StudentAcademicYearMismatch = Prefix + "StudentAcademicYearMismatch";
    public const string InvalidDueFee = Prefix + "InvalidDueFee";
}
