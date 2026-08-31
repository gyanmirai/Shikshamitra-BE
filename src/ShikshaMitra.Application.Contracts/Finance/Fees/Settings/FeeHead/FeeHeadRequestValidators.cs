using FluentValidation;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadCreateRequestValidator : AbstractValidator<FeeHeadCreateRequest>
{
    public FeeHeadCreateRequestValidator()
    {
        RuleFor(x => x.FeeCategoryId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(3, 40);

        RuleFor(x => x.Module)
            .IsInEnum()
            .When(x => x.Module.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

public class FeeHeadUpdateRequestValidator : AbstractValidator<FeeHeadUpdateRequest>
{
    public FeeHeadUpdateRequestValidator()
    {
        RuleFor(x => x.FeeCategoryId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(3, 40);

        RuleFor(x => x.Module)
            .IsInEnum()
            .When(x => x.Module.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

public class FeeHeadStatusUpdateRequestValidator : AbstractValidator<FeeHeadStatusUpdateRequest>
{
    public FeeHeadStatusUpdateRequestValidator()
    {
        RuleFor(x => x.DeactivationReason)
            .MaximumLength(500);
    }
}
