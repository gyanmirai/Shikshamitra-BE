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
}
