using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>Input DTO to transition a refund request's status.</summary>
public class UpdateFeeRefundStatusDto
{
    [Required]
    public FeeRefundStatus Status { get; set; }

    /// <summary>Required when rejecting; optional when approving or cancelling.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateFeeRefundStatusDtoValidator : AbstractValidator<UpdateFeeRefundStatusDto>
{
    public UpdateFeeRefundStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .Must(s => s is FeeRefundStatus.Approved
                          or FeeRefundStatus.Rejected
                          or FeeRefundStatus.UnderReview
                          or FeeRefundStatus.Cancelled)
            .WithMessage("Only Approved, Rejected, UnderReview, or Cancelled transitions are allowed via this endpoint.");

        RuleFor(x => x.Notes)
            .NotEmpty()
            .When(x => x.Status == FeeRefundStatus.Rejected)
            .WithMessage("Notes are required when rejecting a refund request.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes != null);
    }
}
