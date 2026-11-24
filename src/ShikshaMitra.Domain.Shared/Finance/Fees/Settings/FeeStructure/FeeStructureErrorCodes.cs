namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public static class FeeStructureErrorCodes
{
	public const string InvalidConfiguration = "Finance:FeeStructure:InvalidConfiguration";
	public const string CannotDeleteInUse = "Finance:FeeStructure:CannotDeleteInUse";
	public const string ScopeMismatch = "Finance:FeeStructure:ScopeMismatch";
	public const string VersionMismatch = "Finance:FeeStructure:VersionMismatch";
	public const string ConcurrencyConflict = "Finance:FeeStructure:ConcurrencyConflict";
	public const string DuplicateName = "Finance:FeeStructure:DuplicateName";
	public const string UnchangedRevisionFeeHeads = "Finance:FeeStructure:UnchangedRevisionFeeHeads";
	public const string RevisionRequiresPublication = "Finance:FeeStructure:RevisionRequiresPublication";
}
