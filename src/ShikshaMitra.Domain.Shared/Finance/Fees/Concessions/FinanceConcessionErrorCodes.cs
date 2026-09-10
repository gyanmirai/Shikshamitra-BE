namespace ShikshaMitra.Finance.Fees.Concessions;

public static class FinanceConcessionErrorCodes
{
	public const string NotPending = "Finance:Concession:010001";
	public const string DiscountExceedsBase = "Finance:Concession:010002";
	public const string PercentageOver100 = "Finance:Concession:010003";
	public const string MissingRequiredDocuments = "Finance:Concession:010004";
	public const string CannotApproveOrRejectNonPending = "Finance:Concession:010005";
	public const string DuplicatePendingRequest = "Finance:Concession:010010";
	public const string ConcessionOnFullyPaidDueFee = "Finance:Concession:010011";
	public const string AllocationSumMismatch = "Finance:Concession:010012";
	public const string ConcessionExceedsDueOutstanding = "Finance:Concession:010013";
	public const string InvalidRejectionReason = "Finance:Concession:010014";
	public const string ReversalExceedsConcessionBalance = "Finance:Concession:010015";
	public const string InvalidReversal = "Finance:Concession:010016";
	public const string InvalidDocumentSubmission = "Finance:Concession:010017";

	public const string StudentNotFound = "Finance:Concession:020002";
	public const string ConcessionTypeNotFound = "Finance:Concession:020003";
	public const string ScopeMismatch = "Finance:Concession:020005";
	public const string FeeHeadNotInStructure = "Finance:Concession:020006";
	public const string DueFeeNotFound = "Finance:Concession:020007";
	public const string DueFeeWrongStudent = "Finance:Concession:020008";
	public const string DueFeeWrongFeeHead = "Finance:Concession:020009";

	public const string ApproverCannotBeRequestor = "Finance:Concession:030001";
	public const string ConcurrencyStampRequired = "Finance:Concession:030002";
	public const string ConcurrencyConflict = "Finance:Concession:030003";
	public const string ApprovalAllocationsRequired = "Finance:Concession:030004";
}
