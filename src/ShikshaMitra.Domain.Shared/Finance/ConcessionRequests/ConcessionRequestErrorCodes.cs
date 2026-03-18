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
}
