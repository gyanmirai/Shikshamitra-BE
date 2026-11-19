using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Mobile;

public class MobileConcessionInput
{
    public Guid StudentFeeStructureId { get; set; }
    public Guid ConcessionPolicyId { get; set; }
    public Guid ConcessionPolicyVersionId { get; set; }
    public Guid ConcessionTypeId { get; set; }
    [MaxLength(1000)] public string? Remarks { get; set; }
    [MaxLength(100)] public string? IdempotencyKey { get; set; }
    public string? PreviewFingerprint { get; set; }
    public string? ConcurrencyStamp { get; set; }
    [Required] public List<MobileConcessionFeeHeadInput> FeeHeadConcessions { get; set; } = [];
    [Required] public List<MobileConcessionDueInput> DueFeeConcessions { get; set; } = [];
    [Required] public List<MobileConcessionDocumentInput> Documents { get; set; } = [];
}

public class MobileConcessionFeeHeadInput
{
    public Guid FeeHeadId { get; set; }
    public int BasedOn { get; set; }
    public string Amount { get; set; } = "0.00";
}

public class MobileConcessionDueInput
{
    public Guid DueFeeId { get; set; }
    public Guid FeeHeadId { get; set; }
    public string Amount { get; set; } = "0.00";
}

public class MobileConcessionDocumentInput
{
    public Guid DocumentId { get; set; }
    [MaxLength(500)] public string Url { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
}

public class MobileFinanceRequestAction
{
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public string RequestConcurrencyStamp { get; set; } = string.Empty;
    public string? PreviewFingerprint { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }
}

public class MobileRefundInput
{
    public Guid OriginalReceiptId { get; set; }
    [Required] public List<MobileRefundLineInput> Lines { get; set; } = [];
    [MaxLength(100)] public string RefundMethod { get; set; } = string.Empty;
    public Dictionary<string, string>? PaymentDetails { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }
    [MaxLength(100)] public string? IdempotencyKey { get; set; }
    public string? PreviewFingerprint { get; set; }
}

public class MobileRefundLineInput
{
    public Guid? DueFeeId { get; set; }
    public Guid SourceAllocationId { get; set; }
    public string Amount { get; set; } = "0.00";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ChargeId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? LateFeeDueFeeId { get; set; }
}

public class MobileAdjustmentInput
{
    [MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public List<MobileAdjustmentLineInput> SourceLines { get; set; } = [];
    [MaxLength(100)] public string? IdempotencyKey { get; set; }
    public string? PreviewFingerprint { get; set; }
}

public class MobileAdjustmentLineInput
{
    public Guid DueFeeId { get; set; }
    public string Amount { get; set; } = "0.00";
}

public sealed record MobileFinanceCommandResult(Guid Id, string Kind, string Status, string Amount, IReadOnlyList<string> AvailableActions) { public string Currency => "INR"; }
public sealed record MobileFinanceActivityItem(Guid Id, string Kind, string Title, string Status, DateTime Date, bool IsRead);

public sealed record MobileAdjustmentEligibility(IReadOnlyList<MobileAdjustmentSource> Lines) { public string Currency => "INR"; }
public sealed record MobileAdjustmentSource(Guid DueFeeId, Guid FeeHeadId, string Label, string AvailableAmount);
public sealed record MobileAdjustmentPreview(string PreviewFingerprint, string Amount) { public string Currency => "INR"; }
