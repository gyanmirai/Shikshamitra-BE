using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>
/// Input DTO for updating an existing fee-refund request.
/// Only allowed while the request is in <see cref="FeeRefundStatus.Pending"/>.
///
/// The frontend sends the same <c>IFeeRefundApiPayload</c> shape for both POST and PUT,
/// so this DTO intentionally mirrors <see cref="CreateFeeRefundRequestDto"/>.
/// <c>StudentId</c> and <c>AcademicYearId</c> are accepted but treated as read-only
/// (the route <c>id</c> identifies the record; these are silently ignored to keep the
/// frontend payload symmetric).
/// </summary>
public class UpdateFeeRefundRequestDto
{
    /// <summary>Accepted for payload symmetry with create; the route id is authoritative.</summary>
    public Guid? StudentId { get; set; }

    /// <summary>Accepted for payload symmetry; ownership is derived from the stored request.</summary>
    public Guid? FinanceFeeAccountId { get; set; }

    /// <summary>Accepted for payload symmetry with create; the route id is authoritative.</summary>
    public Guid AcademicYearId { get; set; }

    public Guid OriginalReceiptId { get; set; }

    /// <summary>Full replacement of refund lines — at least one is required.</summary>
    [Required]
    [MinLength(1)]
    public List<CreateFeeRefundLineDto> Lines { get; set; } = [];

    /// <summary>
    /// Payment method key for disbursement.
    /// Accepted values: "Cash", "Cheque", "Demand Draft (DD)",
    /// "Bank Transfer (NEFT/RTGS/IMPS)", "UPI".
    /// </summary>
    [MaxLength(100)]
    public string? RefundMethod { get; set; }

    /// <summary>Flat key/value map of method-specific disbursement fields.</summary>
    public Dictionary<string, object?>? PaymentDetails { get; set; }

    /// <summary>Optional reason / taxonomy for the refund.</summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

// ── FluentValidation validator ────────────────────────────────────────────────

public class UpdateFeeRefundRequestDtoValidator : AbstractValidator<UpdateFeeRefundRequestDto>
{
    private static readonly HashSet<string> AllowedMethods =
    [
        "Cash",
        "Cheque",
        "Demand Draft (DD)",
        "Bank Transfer (NEFT/RTGS/IMPS)",
        "UPI",
    ];

    public UpdateFeeRefundRequestDtoValidator()
    {
        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one refund line is required.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.DueFeeId).NotEmpty();
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

        RuleFor(x => x.PaymentDetails)
            .NotNull()
            .WithMessage("PaymentDetails are required for the selected refund method.")
            .When(x => !string.IsNullOrWhiteSpace(x.RefundMethod));
    }
}
