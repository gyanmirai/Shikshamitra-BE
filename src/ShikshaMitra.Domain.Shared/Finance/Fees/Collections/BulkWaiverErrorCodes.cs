namespace ShikshaMitra.Domain.Finance.Policies;

public static class BulkWaiverErrorCodes
{
	private const string Prefix = "Finance:BulkWaiver:";

	public const string ScopeIdRequired = Prefix + "010001";
	public const string ReasonRequired = Prefix + "010002";
	public const string NotPendingApproval = Prefix + "010003";
	public const string NotApproved = Prefix + "010004";
	public const string NoAffectedDueLines = Prefix + "010005";
}
