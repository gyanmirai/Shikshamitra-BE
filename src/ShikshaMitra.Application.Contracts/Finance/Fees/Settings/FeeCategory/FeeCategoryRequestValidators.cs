using System.Linq;
using FluentValidation;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadItemRequestValidator : AbstractValidator<FeeHeadItemRequest>
{
    public FeeHeadItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(3, 40);

        RuleFor(x => x.Module).IsInEnum();

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

public class FeeCategoryCreateRequestValidator : AbstractValidator<FeeCategoryCreateRequest>
{
    public FeeCategoryCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(3, 100);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.FeeHeads)
            .NotEmpty()
            .WithMessage("At least one fee head is required.")
            .Must(heads => heads.Select(h => h.Name.Trim().ToLowerInvariant()).Distinct().Count() == heads.Count)
            .WithMessage("Duplicate fee head names are not allowed.");

        RuleForEach(x => x.FeeHeads).SetValidator(new FeeHeadItemRequestValidator());
    }
}

public class FeeCategoryUpdateRequestValidator : AbstractValidator<FeeCategoryUpdateRequest>
{
    public FeeCategoryUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(3, 100);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.FeeHeads)
            .NotEmpty()
            .WithMessage("At least one fee head is required.")
            .Must(heads => heads.Select(h => h.Name.Trim().ToLowerInvariant()).Distinct().Count() == heads.Count)
            .WithMessage("Duplicate fee head names are not allowed.");

        RuleForEach(x => x.FeeHeads).SetValidator(new FeeHeadItemRequestValidator());
    }
}

public class FeeCategoryStatusUpdateRequestValidator : AbstractValidator<FeeCategoryStatusUpdateRequest>
{
    public FeeCategoryStatusUpdateRequestValidator()
    {
        RuleFor(x => x.DeactivationReason)
            .MaximumLength(500);
    }
}
