using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>Input DTO for creating a new fee-refund request.</summary>
public class CreateFeeRefundRequestDto
{
    [Required]
    public Guid StudentId { get; set; }

    [Required]
    public Guid AcademicYearId { get; set; }

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

/// <summary>One refund line pointing to a specific DueFee row.</summary>
public class CreateFeeRefundLineDto
{
    [Required]
    public Guid DueFeeId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }
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
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.AcademicYearId).NotEmpty();

        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one refund line is required.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.DueFeeId).NotEmpty();
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });

        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .When(x => x.Reason != null);

        RuleFor(x => x.RefundMethod)
            .MaximumLength(100)
            .Must(m => m == null || AllowedMethods.Contains(m))
            .WithMessage($"RefundMethod must be one of: {string.Join(", ", AllowedMethods)}.")
            .When(x => x.RefundMethod != null);

        // Non-Cash methods require payment details
        RuleFor(x => x.PaymentDetails)
            .NotNull()
            .WithMessage("PaymentDetails are required for the selected refund method.")
            .When(x => x.RefundMethod != null && x.RefundMethod != "Cash");
    }
}
