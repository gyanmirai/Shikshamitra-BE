namespace ShikshaMitra.Finance.Fees.Settings;

public static class FinanceFeeHeadErrorCodes
{
	private const string Prefix = "Finance:FeeHead";

	public const string CannotDeleteInUse = $"{Prefix}:010001";
	public const string CannotDeactivateInUse = $"{Prefix}:010002";
	public const string DuplicateName = $"{Prefix}:010003";
	public const string NotFound = $"{Prefix}:010004";
	public const string ConcurrencyConflict = $"{Prefix}:010005";
	public const string RevisionRequired = $"{Prefix}:010006";
	public const string LastHeadCannotBeDeleted = $"{Prefix}:010007";
	public const string UnavailableForNewSetup = $"{Prefix}:010008";
	public const string RevisionNotRequired = $"{Prefix}:010009";
	public const string DeactivationReasonRequired = $"{Prefix}:010010";
}
