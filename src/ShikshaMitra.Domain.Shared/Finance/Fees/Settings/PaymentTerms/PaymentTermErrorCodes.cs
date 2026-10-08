namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public static class PaymentTermErrorCodes
{
	private const string Prefix = "Finance:PaymentTerm";

	public const string NoInstallments = $"{Prefix}:000001";
	public const string DuplicateOrOutOfOrderSequence = $"{Prefix}:000002";
	public const string DuplicateInstallmentName = $"{Prefix}:000003";
	public const string OverlappingCoverage = $"{Prefix}:000004";
	public const string CoverageStartAfterEnd = $"{Prefix}:000005";
	public const string DemandDateAfterDueDate = $"{Prefix}:000006";
	public const string PercentageTotalNot100 = $"{Prefix}:000007";
	public const string AllocationPercentageMustBePositive = $"{Prefix}:000009";
	public const string NoDraftVersion = $"{Prefix}:000010";
	public const string VersionNotEditable = $"{Prefix}:000011";
	public const string VersionNotPublishable = $"{Prefix}:000012";
	public const string ArchiveInUse = $"{Prefix}:000013";
	public const string DeleteWithReferences = $"{Prefix}:000014";
	public const string ChangeReasonRequired = $"{Prefix}:000015";
	public const string TermYearMismatch = $"{Prefix}:000016";
	public const string TermBoardMismatch = $"{Prefix}:000017";
	public const string VersionNotPublished = $"{Prefix}:000018";
	public const string ScheduleLockedByIssuedDues = $"{Prefix}:000019";
	public const string DuplicateScheduleInstance = $"{Prefix}:000020";
	public const string NameRequired = $"{Prefix}:000021";
	public const string CodeRequired = $"{Prefix}:000022";
	public const string DuplicateCode = $"{Prefix}:000023";
	public const string DemandOffsetDaysMustBeNonNegative = $"{Prefix}:000024";
	public const string CompulsoryHeadRequiresTerm = $"{Prefix}:000027";
	public const string NotFound = $"{Prefix}:000028";
	public const string ArchiveReasonRequired = $"{Prefix}:000029";
	public const string AdoptionReasonRequired = $"{Prefix}:000030";
	public const string AdoptionTargetNotPublished = $"{Prefix}:000031";
	public const string AllocationPercentageExceeds100 = $"{Prefix}:000032";
	public const string DemandDateRequired = $"{Prefix}:000033";
	public const string DueDateOutsideCoverage = $"{Prefix}:000034";
	public const string InstallmentDateOutsideBoardPeriod = $"{Prefix}:000035";
	public const string EffectiveDateRequired = $"{Prefix}:000036";
	public const string DefaultTermRequired = $"{Prefix}:000037";
	public const string DefaultTermMustBeActive = $"{Prefix}:000038";
	public const string InvalidTemplateConfiguration = $"{Prefix}:000039";
	public const string DemandDateBeforeCoverageStart = $"{Prefix}:000040";
	public const string DemandOffsetDaysExceedsMaximum = $"{Prefix}:000041";
}
