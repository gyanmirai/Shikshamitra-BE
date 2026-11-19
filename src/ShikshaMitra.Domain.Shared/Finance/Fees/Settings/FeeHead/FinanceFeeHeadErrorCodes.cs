namespace ShikshaMitra.Finance.Fees.Settings;

public static class FinanceFeeHeadErrorCodes
{
	private const string Prefix = "Finance:FeeHead";

	public const string CannotDeleteInUse = $"{Prefix}:010001";
	public const string CannotDeactivateInUse = $"{Prefix}:010002";
	public const string DuplicateName = $"{Prefix}:010003";
	public const string NotFound = $"{Prefix}:010004";
	public const string InvalidIncomeCategory = $"{Prefix}:010005";
	public const string IncomeCategoryChangeBlocked = $"{Prefix}:010006";
	public const string CategoryArchived = $"{Prefix}:010007";
}
