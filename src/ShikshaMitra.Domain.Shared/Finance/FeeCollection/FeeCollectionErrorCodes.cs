namespace ShikshaMitra.Finance.FeeCollection;

public static class FeeCollectionErrorCodes
{
    private const string Prefix = "ShikshaMitra:FeeCollection:";

    public const string PaymentAmountMustBePositive = Prefix + "PaymentAmountMustBePositive";
    public const string PaymentDetailsRequiredForNonCash = Prefix + "PaymentDetailsRequiredForNonCash";
    public const string PaymentDetailsMismatch = Prefix + "PaymentDetailsMismatch";
}
