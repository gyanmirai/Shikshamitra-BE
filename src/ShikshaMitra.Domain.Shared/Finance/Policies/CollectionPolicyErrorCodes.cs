namespace ShikshaMitra.Domain.Finance.Policies;

public static class CollectionPolicyErrorCodes
{
	private const string Prefix = "Finance:CollectionPolicy:";

	public const string PolicyNotFound = Prefix + "010001";
	public const string NameRequired = Prefix + "010002";
	public const string AcademicYearRequired = Prefix + "010003";
	public const string InvalidEscalationSlabs = Prefix + "010004";
	public const string DuplicateDefaultForAcademicYear = Prefix + "010005";
	public const string DefaultPolicyMustBeActive = Prefix + "010006";
	public const string ArchivedPolicyNotEditable = Prefix + "010007";
	public const string AcademicYearCannotChange = Prefix + "010008";
	public const string StudentOverrideNotFound = Prefix + "010009";
	public const string WaiverPreApprovalReasonRequired = Prefix + "010010";
	public const string DuplicateFeeHeadOverride = Prefix + "010011";
	public const string CustomRuleRequiresLateFeeRule = Prefix + "010012";
	public const string ExceptionWindowScopeIdRequired = Prefix + "010013";
	public const string ExceptionWindowInvalidDateRange = Prefix + "010014";
	public const string ExceptionWindowExtensionDaysRequired = Prefix + "010015";
	public const string ExceptionWindowReasonRequired = Prefix + "010016";
	public const string ExceptionWindowAlreadyActive = Prefix + "010017";
	public const string ExceptionWindowNotFound = Prefix + "010018";
}
