namespace ShikshaMitra.Finance.ConcessionRequests;

public static class ConcessionRequestErrorCodes
{
    private const string Prefix = "ShikshaMitra:ConcessionRequest:";

    public const string NotFound = Prefix + "NotFound";
    public const string CannotSubmitNonDraft = Prefix + "CannotSubmitNonDraft";
    public const string CannotApproveNonPending = Prefix + "CannotApproveNonPending";
    public const string CannotRejectNonPending = Prefix + "CannotRejectNonPending";
    public const string CannotUpdateNonDraft = Prefix + "CannotUpdateNonDraft";
    public const string CannotDeleteNonDraft = Prefix + "CannotDeleteNonDraft";
    public const string NoFeeHeadMappings = Prefix + "NoFeeHeadMappings";
    public const string DuplicateFeeHead = Prefix + "DuplicateFeeHead";

    /// <summary>A non-rejected request for the same student + concession type already exists.</summary>
    public const string DuplicateActiveRequest = Prefix + "DuplicateActiveRequest";

    /// <summary>The supplied BoardId does not match the student's enrolled board.</summary>
    public const string StudentBoardMismatch = Prefix + "StudentBoardMismatch";

    /// <summary>The supplied EducationMediumId does not match the student's enrolled medium.</summary>
    public const string StudentMediumMismatch = Prefix + "StudentMediumMismatch";

    /// <summary>The supplied StandardId does not match the student's enrolled standard.</summary>
    public const string StudentStandardMismatch = Prefix + "StudentStandardMismatch";

    /// <summary>The supplied DivisionId does not match the student's enrolled division.</summary>
    public const string StudentDivisionMismatch = Prefix + "StudentDivisionMismatch";

    /// <summary>The concession type was not found.</summary>
    public const string ConcessionTypeNotFound = Prefix + "ConcessionTypeNotFound";

    /// <summary>
    /// The anchor request used for the update is not in Pending status.
    /// Only Pending requests may be updated.
    /// </summary>
    public const string CannotUpdateNonPending = Prefix + "CannotUpdateNonPending";

    /// <summary>
    /// The studentId in the request body does not match the student on the
    /// concession request identified by the path parameter.
    /// </summary>
    public const string StudentRequestOwnershipMismatch = Prefix + "StudentRequestOwnershipMismatch";

    /// <summary>
    /// A feeHeadId supplied in the fee-heads data does not exist in the system.
    /// </summary>
    public const string FeeHeadNotFound = Prefix + "FeeHeadNotFound";

    /// <summary>
    /// The input references a concessionTypeId for which there is no corresponding
    /// Draft ConcessionRequest for this student.
    /// </summary>
    public const string ConcessionTypeMissingFromDraftBatch = Prefix + "ConcessionTypeMissingFromDraftBatch";

    /// <summary>
    /// The same feeHeadId appears more than once within a single concession type's
    /// fee-heads selection.
    /// </summary>
    public const string DuplicateFeeHeadInConcessionType = Prefix + "DuplicateFeeHeadInConcessionType";

    // ── Step 4 (Value Entry) ─────────────────────────────────────────────────

    /// <summary>
    /// A mapped fee head (saved in Step 3) is missing from the Step 4 value-entry payload.
    /// All fee heads mapped to a request must appear in the submission.
    /// </summary>
    public const string FeeHeadMissingFromValueEntry = Prefix + "FeeHeadMissingFromValueEntry";

    /// <summary>
    /// The Step 4 payload includes a fee head that was never mapped to this concession
    /// request in Step 3.
    /// </summary>
    public const string FeeHeadNotMappedToRequest = Prefix + "FeeHeadNotMappedToRequest";

    /// <summary>
    /// A concession type from the student's batch is absent from the Step 4 payload.
    /// All concession types must be submitted together.
    /// </summary>
    public const string ConcessionTypeMissingFromValueEntry = Prefix + "ConcessionTypeMissingFromValueEntry";

    /// <summary>
    /// A percentage discount value exceeds 100. Percentages must be in [0, 100].
    /// </summary>
    public const string InvalidPercentageAmount = Prefix + "InvalidPercentageAmount";

    /// <summary>
    /// A flat discount amount exceeds the fee head's annual base amount.
    /// The discount cannot be greater than what the student owes.
    /// </summary>
    public const string ExcessiveDiscountAmount = Prefix + "ExcessiveDiscountAmount";

    /// <summary>
    /// The <c>basedOn</c> discriminator is not a recognised value (must be 0 or 1).
    /// </summary>
    public const string InvalidBasedOnValue = Prefix + "InvalidBasedOnValue";

    // ── Step 5 (Document Upload) ─────────────────────────────────────────────

    /// <summary>
    /// Document uploads can only be saved while the request is in Draft status.
    /// </summary>
    public const string CannotUploadDocumentsForNonDraft = Prefix + "CannotUploadDocumentsForNonDraft";

    /// <summary>
    /// A concession type from the student's batch is absent from the document-upload payload.
    /// All concession types must be submitted together.
    /// </summary>
    public const string ConcessionTypeMissingFromDocumentUpload = Prefix + "ConcessionTypeMissingFromDocumentUpload";

    /// <summary>
    /// The same documentId appears more than once within a single concession type's
    /// document-upload block.
    /// </summary>
    public const string DuplicateDocumentInConcessionType = Prefix + "DuplicateDocumentInConcessionType";

    /// <summary>
    /// The <c>status</c> discriminator is not a recognised value (must be 0 = Pending or 1 = Uploaded).
    /// </summary>
    public const string InvalidDocumentStatus = Prefix + "InvalidDocumentStatus";
}
