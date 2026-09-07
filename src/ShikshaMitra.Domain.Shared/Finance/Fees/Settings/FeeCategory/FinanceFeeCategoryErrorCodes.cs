namespace ShikshaMitra.Finance.Fees.Settings;

public static class FinanceFeeCategoryErrorCodes
{
	private const string Prefix = "Finance:FeeCategory";

	public const string CannotDeleteInUse = $"{Prefix}:010001";
	public const string DuplicateName = $"{Prefix}:010002";
	public const string NotFound = $"{Prefix}:010003";
	public const string DuplicateFeeHeadName = $"{Prefix}:010004";
	public const string AtLeastOneFeeHeadRequired = $"{Prefix}:010005";
	public const string InvalidQuery = $"{Prefix}:010006";
	public const string ConcurrencyConflict = $"{Prefix}:010007";
	public const string CopyConflict = $"{Prefix}:010008";
	public const string ActiveCategoryRequiresFeeHead = $"{Prefix}:010009";
	public const string DeactivationReasonRequired = $"{Prefix}:010010";
}
