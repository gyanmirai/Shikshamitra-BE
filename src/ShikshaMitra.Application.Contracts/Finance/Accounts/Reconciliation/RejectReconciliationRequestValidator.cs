using FluentValidation;

namespace ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;

public class RejectReconciliationRequestValidator : AbstractValidator<RejectReconciliationInput>
{
    public RejectReconciliationRequestValidator()
    {
        RuleFor(x => x.Remarks)
            .NotEmpty()
            .WithMessage("Remarks are required when rejecting a reconciliation entry.")
            .MaximumLength(1000)
            .WithMessage("Remarks must not exceed 1000 characters.");
    }
}
