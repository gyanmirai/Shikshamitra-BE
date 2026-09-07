namespace ShikshaMitra.Domain.Finance.Policies;

public static class ConcessionPolicyErrorCodes
{
	private const string Prefix = "Finance:ConcessionPolicy:";

	public const string PolicyNotFound = Prefix + "010001";
	public const string NameRequired = Prefix + "010002";
	public const string CodeRequired = Prefix + "010003";
	public const string AcademicYearRequired = Prefix + "010004";
	public const string DuplicateName = Prefix + "010005";
	public const string DuplicateCode = Prefix + "010006";
	public const string InvalidStatus = Prefix + "010007";
	public const string ArchivedPolicyNotEditable = Prefix + "010008";
	public const string AcademicYearCannotChange = Prefix + "010009";
	public const string PolicyNotActive = Prefix + "010010";
	public const string PolicyNotAttached = Prefix + "010011";
	public const string PolicyVersionNotFound = Prefix + "010012";
	public const string FeeHeadRequired = Prefix + "010013";
	public const string InvalidCalculationRule = Prefix + "010014";
	public const string LimitExceeded = Prefix + "010015";
	public const string InputNotAllowed = Prefix + "010016";
	public const string AcademicYearMismatch = Prefix + "010017";
	public const string PolicyInUse = Prefix + "010018";
}
