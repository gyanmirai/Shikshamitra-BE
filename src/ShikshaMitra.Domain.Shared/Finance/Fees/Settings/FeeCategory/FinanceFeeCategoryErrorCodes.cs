namespace ShikshaMitra.Finance.Fees.Settings;

public static class FinanceFeeCategoryErrorCodes
{
	private const string Prefix = "Finance:FeeCategory";

	public const string CannotDeleteInUse = $"{Prefix}:010001";
	public const string DuplicateName = $"{Prefix}:010002";
	public const string NotFound = $"{Prefix}:010003";
	public const string DuplicateFeeHeadName = $"{Prefix}:010004";
	public const string AtLeastOneFeeHeadRequired = $"{Prefix}:010005";
}
