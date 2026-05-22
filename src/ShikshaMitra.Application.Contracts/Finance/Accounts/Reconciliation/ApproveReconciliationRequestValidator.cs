using FluentValidation;

namespace ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;

public class ApproveReconciliationRequestValidator : AbstractValidator<ApproveReconciliationInput>
{
    public ApproveReconciliationRequestValidator()
    {
        RuleFor(x => x.Remarks)
            .MaximumLength(1000)
            .WithMessage("Remarks must not exceed 1000 characters.");
    }
}
