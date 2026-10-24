namespace ShikshaMitra.Domain.Finance.Policies;

public static class CollectionPolicyErrorCodes
{
	public const string InvalidAllocationBasis = "Finance:CollectionPolicy:InvalidAllocationBasis";
	public const string DefaultPolicyCannotBeRemoved = "Finance:CollectionPolicy:010033";
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
	public const string InvalidStatus = Prefix + "010019";
	public const string CodeRequired = Prefix + "010020";
	public const string InvalidEffectiveDates = Prefix + "010021";
	public const string InvalidLateFeeRule = Prefix + "010022";
	public const string InvalidWaiverRule = Prefix + "010023";
	public const string InvalidFeeHeadReference = Prefix + "010024";
	public const string PolicyNotActive = Prefix + "010025";
	public const string AcademicYearMismatch = Prefix + "010026";
	public const string PolicyVersionNotFound = Prefix + "010027";
	public const string PolicyNotAttached = Prefix + "010028";
	public const string PolicyInUse = Prefix + "010029";
	public const string FeeHeadMismatch = Prefix + "010030";
	public const string DuplicateName = Prefix + "010034";
	public const string DuplicatePolicyCode = Prefix + "010032";
}
