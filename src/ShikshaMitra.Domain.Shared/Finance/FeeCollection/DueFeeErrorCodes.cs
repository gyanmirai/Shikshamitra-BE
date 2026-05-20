namespace ShikshaMitra.Finance.FeeCollection;

public static class DueFeeErrorCodes
{
	private const string Prefix = "ShikshaMitra:DueFee:";

	public const string DueFeesAlreadyGenerated       = Prefix + "10001";
	public const string PaymentAmountMustBePositive    = Prefix + "10002";
	public const string RefundAmountMustBePositive     = Prefix + "10003";
	public const string RefundExceedsPaidAmount        = Prefix + "10004";
	public const string DueFeeNotFound                 = Prefix + "10005";
}
