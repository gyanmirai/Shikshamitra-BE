namespace ShikshaMitra.Finance.Fees.FeeCollection;

public static class LateFeeWaiverErrorCodes
{
	private const string Prefix = "Finance:LateFeeWaiver:";

	public const string ReasonRequired = Prefix + "010001";
	public const string NotPendingApproval = Prefix + "010002";
	public const string DueFeeNotFound = Prefix + "010003";
	public const string NoCollectionPolicyResolved = Prefix + "010004";
	public const string RequestedAmountMustBePositive = Prefix + "010005";
}
