using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

public class FeeRefundActionDto
{
    public Guid? ExpenseCategoryId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? Method { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class RejectFeeRefundDto
{
    [Required, MaxLength(1000)] public string Notes { get; set; } = string.Empty;
}

public class ProcessFeeRefundDto
{
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Method { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal Amount { get; set; }
    public DateTimeOffset SettlementDate { get; set; }
    [MaxLength(200)] public string? ExternalReference { get; set; }
    public Dictionary<string, object?>? PaymentDetails { get; set; }
}

public class RecordRefundPayoutDto
{
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Method { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal Amount { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public Guid? BankAccountId { get; set; }
    [MaxLength(200)] public string? ExternalReference { get; set; }
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
}

public class ResolveRefundPayoutDto
{
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Reason { get; set; }
}

public class ReturnRefundPayoutDto
{
    [Required] public Guid SettlementId { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
}

public class ReverseFeeRefundDto
{
    [Required] public Guid SettlementId { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public class FeeRefundPreviewDto
{
    public Guid StudentFeeStructureId { get; set; }
    public Guid RefundPolicyId { get; set; }
    public Guid RefundPolicyVersionId { get; set; }
    public string RefundPolicyName { get; set; } = string.Empty;
    public decimal SettledPaidAmount { get; set; }
    public decimal PriorProcessedRefund { get; set; }
    public decimal ActiveReservation { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal DeductionAmount { get; set; }
    public decimal CapAdjustment { get; set; }
    public decimal MaximumAllowedAmount { get; set; }
    public bool IsAllowed { get; set; }
    public List<string> Reasons { get; set; } = [];
}

public class ProcessFeeRefundDtoValidator : AbstractValidator<ProcessFeeRefundDto>
{
    public ProcessFeeRefundDtoValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Method).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.SettlementDate).NotEmpty();
    }
}
