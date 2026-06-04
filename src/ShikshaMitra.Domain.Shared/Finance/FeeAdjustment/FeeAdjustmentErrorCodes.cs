namespace ShikshaMitra.Finance.FeeAdjustment;

public static class FeeAdjustmentErrorCodes
{
    public const string Namespace = "Finance:FeeAdjustment";

    public const string NotFound = Namespace + ":NotFound";
    public const string NotPending = Namespace + ":NotPending";
    public const string IllegalStatusTransition = Namespace + ":IllegalStatusTransition";
    public const string ApproverCannotBeRequestor = Namespace + ":ApproverCannotBeRequestor";
    public const string SourceLineInvalid = Namespace + ":SourceLineInvalid";
    public const string SourceExceedsAdjustableBalance = Namespace + ":SourceExceedsAdjustableBalance";
    public const string TargetExceedsOutstanding = Namespace + ":TargetExceedsOutstanding";
    public const string ApplyExceedsRemaining = Namespace + ":ApplyExceedsRemaining";
    public const string NoAllocationLines = Namespace + ":NoAllocationLines";
    public const string AlreadyFullyApplied = Namespace + ":AlreadyFullyApplied";
    public const string DuplicateIdempotencyKey = Namespace + ":DuplicateIdempotencyKey";
    public const string DueFeeNotFound = Namespace + ":DueFeeNotFound";
    public const string StudentNotFound = Namespace + ":StudentNotFound";
    public const string CannotApply = Namespace + ":CannotApply";
}
