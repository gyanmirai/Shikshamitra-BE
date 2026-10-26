using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>Input DTO for creating a new fee-refund request.</summary>
public class CreateFeeRefundRequestDto
{
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? ReplacesRefundRequestId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }

    [Required]
    public Guid AcademicYearId { get; set; }

    [Required]
    public Guid OriginalReceiptId { get; set; }

    /// <summary>At least one refund line is required.</summary>
    [Required]
    [MinLength(1)]
    public List<CreateFeeRefundLineDto> Lines { get; set; } = [];

    /// <summary>
    /// Payment method key that determines how the refund will be disbursed.
    /// Accepted values: "Cash", "Cheque", "Demand Draft (DD)",
    /// "Bank Transfer (NEFT/RTGS/IMPS)", "UPI".
    /// </summary>
    [MaxLength(100)]
    public string? RefundMethod { get; set; }

    /// <summary>
    /// Flat key/value dictionary carrying the method-specific disbursement fields.
    /// The required keys vary by <see cref="RefundMethod"/> — mirrors the frontend
    /// <c>paymentDetails</c> object (e.g., chequeNumber, upiId, beneficiaryAccountNumber).
    /// </summary>
    public Dictionary<string, object?>? PaymentDetails { get; set; }

    /// <summary>Optional reason / taxonomy for the refund (TC, excess payment, transfer, etc.).</summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>One refund line identifying either a fee due or a paid charge and its payment allocation.</summary>
public class CreateFeeRefundLineDto
{
    public Guid? ChargeId { get; set; }
    /// <summary>Due line whose collected late fee is being refunded; distinct from principal.</summary>
    public Guid? LateFeeDueFeeId { get; set; }
    public Guid? DueFeeId { get; set; }

    [Required]
    public Guid SourceAllocationId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }
}

// ── FluentValidation validator ────────────────────────────────────────────────

public class CreateFeeRefundRequestDtoValidator : AbstractValidator<CreateFeeRefundRequestDto>
{
    private static readonly HashSet<string> AllowedMethods =
    [
        "Cash",
        "Cheque",
        "Demand Draft (DD)",
        "Bank Transfer (NEFT/RTGS/IMPS)",
        "UPI",
    ];

    public CreateFeeRefundRequestDtoValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x).Must(x => x.StudentId.HasValue || x.FinanceFeeAccountId.HasValue)
            .WithMessage("A Student or Finance fee account is required.");
        RuleFor(x => x.StudentId).NotEqual(Guid.Empty).When(x => x.StudentId.HasValue);
        RuleFor(x => x.FinanceFeeAccountId).NotEqual(Guid.Empty).When(x => x.FinanceFeeAccountId.HasValue);
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.OriginalReceiptId).NotEmpty();

        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one refund line is required.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l).Must(l => (l.DueFeeId.HasValue ? 1 : 0) + (l.ChargeId.HasValue ? 1 : 0) + (l.LateFeeDueFeeId.HasValue ? 1 : 0) == 1)
                .WithMessage("Specify exactly one fee due, charge, or paid late fee.");
            line.RuleFor(l => l.DueFeeId).NotEqual(Guid.Empty).When(l => l.DueFeeId.HasValue);
            line.RuleFor(l => l.ChargeId).NotEqual(Guid.Empty).When(l => l.ChargeId.HasValue);
            line.RuleFor(l => l.LateFeeDueFeeId).NotEqual(Guid.Empty).When(l => l.LateFeeDueFeeId.HasValue);
            line.RuleFor(l => l.SourceAllocationId).NotEmpty();
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });

        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .When(x => x.Reason != null);

        RuleFor(x => x.RefundMethod)
            .NotEmpty()
            .MaximumLength(100)
            .Must(m => m != null && AllowedMethods.Contains(m))
            .WithMessage($"RefundMethod must be one of: {string.Join(", ", AllowedMethods)}.");

        // Non-Cash methods require payment details
        RuleFor(x => x.PaymentDetails)
            .NotNull()
            .WithMessage("PaymentDetails are required for the selected refund method.")
            .When(x => !string.IsNullOrWhiteSpace(x.RefundMethod));

        RuleFor(x => x).Must(x =>
        {
            bool Has(string key) => x.PaymentDetails != null &&
                x.PaymentDetails.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(value?.ToString());
            return x.RefundMethod switch
            {
                "Cash" => Has("receivedBy"),
                "Cheque" or "Demand Draft (DD)" => Has("payeeName"),
                "Bank Transfer (NEFT/RTGS/IMPS)" => Has("beneficiaryName") &&
                    Has("beneficiaryBankName") && Has("beneficiaryAccountNumber") && Has("ifscCode"),
                "UPI" => Has("beneficiaryName") && Has("upiId"),
                _ => false,
            };
        }).WithMessage("Provide the beneficiary details required for the selected refund method.");
    }
}
