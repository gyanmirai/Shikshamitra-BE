namespace ShikshaMitra.Library;

public static class LibraryFinanceErrorCodes
{
	public const string SettlementRequiresFinance = "Library:Finance:010100";
	public const string InvalidSource = "Library:Finance:010101";
	public const string SourceChanged = "Library:Finance:010102";
	public const string SourceRequiresReview = "Library:Finance:010103";
	public const string InvalidPayer = "Library:Finance:010104";
	public const string InvalidAmount = "Library:Finance:010105";
	public const string UnsupportedCurrency = "Library:Finance:010106";
	public const string SourceAlreadyLinked = "Library:Finance:010107";
	public const string IdempotencyConflict = "Library:Finance:010108";
	public const string InvalidFinanceMapping = "Library:Finance:010109";
	public const string InvalidFinancePolicy = "Library:Finance:010110";
	public const string LegacySettlementRequiresReview = "Library:Finance:010111";
	public const string InvalidBalance = "Library:Finance:010112";
	public const string InvalidBalanceRevision = "Library:Finance:010113";
	public const string IndependentApprovalRequired = "Library:Finance:010114";
}
