namespace ShikshaMitra.Finance.Fees.FeeCollection;

public static class FeeWaiverErrorCodes
{
	private const string Prefix = "Finance:FeeWaiver:";
	public const string ReasonRequired = Prefix + "010001";
	public const string IdempotencyKeyRequired = Prefix + "010002";
	public const string UnsupportedSource = Prefix + "010003";
	public const string SourceNotFound = Prefix + "010004";
	public const string SourceNotEligible = Prefix + "010005";
	public const string NoRemainingBalance = Prefix + "010006";
	public const string IdempotencyConflict = Prefix + "010007";
}
