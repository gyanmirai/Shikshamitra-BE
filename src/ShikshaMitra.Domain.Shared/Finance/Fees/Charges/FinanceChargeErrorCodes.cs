namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Centralized error code catalog for Finance Charges BusinessExceptions.
/// Convention: "Finance:Charge:{6-digit code}"
/// Ranges: Charge lifecycle 01xxxx, Reference data 02xxxx, Authorization 03xxxx
/// </summary>
public static class FinanceChargeErrorCodes
{
	// ── Charge lifecycle: 01xxxx ──────────────────────────────────

	public const string ChargeNotFound = "Finance:Charge:010000";
	public const string ChargeNotPending = "Finance:Charge:010001";
	public const string InvalidChargeAmount = "Finance:Charge:010002";
	public const string InvalidDueDate = "Finance:Charge:010003";
	public const string ChargeCannotBeDeleted = "Finance:Charge:010004";
	public const string InvalidRejectionReason = "Finance:Charge:010005";
	public const string ChargeCategoryInactive = "Finance:Charge:010006";
	public const string InvalidChargeName = "Finance:Charge:010007";
	public const string EmptyChargeAudience = "Finance:Charge:010008";
	public const string InvalidChargeAudienceScope = "Finance:Charge:010009";
	public const string ChargeBatchTooLarge = "Finance:Charge:010010";
	public const string StudentNotInChargeScope = "Finance:Charge:010011";
	public const string ChargeRequestNotFound = "Finance:Charge:010012";
	public const string CannotRemoveAppliedChargeLine = "Finance:Charge:010013";
	public const string InvalidChargeLineSubset = "Finance:Charge:010014";
	public const string ChargeRequestNotFullyPending = "Finance:Charge:010015";
	public const string ConcurrencyStampRequired = "Finance:Charge:010016";
	public const string ConcurrencyConflict = "Finance:Charge:010017";
	public const string ChargeAlreadyCollected = "Finance:Charge:010018";
	public const string ChargeCannotBeVoided = "Finance:Charge:010019";
	public const string OnlineCollectionNotAllowed = "Finance:Charge:010020";
	public const string ChargeCategoryAcademicYearMismatch = "Finance:Charge:010021";
	public const string ChargeCategoryAccountingNotConfigured = "Finance:Charge:010022";
	public const string ChargeCategoryAccountingInvalid = "Finance:Charge:010023";
	public const string ChargeCategoryAccountingLocked = "Finance:Charge:010024";

	// ── Reference data: 02xxxx ────────────────────────────────────

	public const string AcademicYearNotFound = "Finance:Charge:020001";
	public const string StudentNotFound = "Finance:Charge:020002";
	public const string ChargeCategoryNotFound = "Finance:Charge:020003";
	public const string StudentMissingAcademicScope = "Finance:Charge:020004";

	// ── Authorization / maker-checker: 03xxxx ─────────────────────

	public const string ChargeApproverCannotBeRequestor = "Finance:Charge:030001";
}
