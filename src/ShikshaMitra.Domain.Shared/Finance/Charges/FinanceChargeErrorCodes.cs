namespace ShikshaMitra.Finance.Charges;

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

	// ── Reference data: 02xxxx ────────────────────────────────────

	public const string AcademicYearNotFound = "Finance:Charge:020001";
	public const string StudentNotFound = "Finance:Charge:020002";
	public const string ChargeCategoryNotFound = "Finance:Charge:020003";
	public const string StudentMissingAcademicScope = "Finance:Charge:020004";

	// ── Authorization / maker-checker: 03xxxx ─────────────────────

	public const string ChargeApproverCannotBeRequestor = "Finance:Charge:030001";
}
