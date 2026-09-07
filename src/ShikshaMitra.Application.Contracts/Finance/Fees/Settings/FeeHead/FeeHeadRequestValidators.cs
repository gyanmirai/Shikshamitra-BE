using FluentValidation;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadCreateRequestValidator : AbstractValidator<FeeHeadCreateRequest>
{
    public FeeHeadCreateRequestValidator()
    {
        RuleFor(x => x.FeeCategoryId).NotEmpty();

        RuleFor(x => x.Name)
			.Cascade(CascadeMode.Stop)
            .NotEmpty()
			.Must(name => FeeSetupNameNormalizer.ForStorage(name).Length is >= 3 and <= 40)
			.WithMessage("Fee-head name must contain between 3 and 40 characters after whitespace is normalized.")
			.Must(FeeSetupNameNormalizer.HasSensibleCharacters)
			.WithMessage("Fee-head name contains unsupported characters.");

        RuleFor(x => x.Module).IsInEnum();

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
			.Cascade(CascadeMode.Stop)
            .NotEmpty()
			.Must(name => FeeSetupNameNormalizer.ForStorage(name).Length is >= 3 and <= 40)
			.WithMessage("Fee-head name must contain between 3 and 40 characters after whitespace is normalized.")
			.Must(FeeSetupNameNormalizer.HasSensibleCharacters)
			.WithMessage("Fee-head name contains unsupported characters.");

        RuleFor(x => x.Module).IsInEnum();

        RuleFor(x => x.Description)
            .MaximumLength(500);
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

public class FeeHeadStatusUpdateRequestValidator : AbstractValidator<FeeHeadStatusUpdateRequest>
{
    public FeeHeadStatusUpdateRequestValidator()
    {
        RuleFor(x => x.DeactivationReason)
            .NotEmpty().When(x => !x.IsActive)
            .MaximumLength(500);
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

public class FeeHeadRevisionRequestValidator : AbstractValidator<FeeHeadRevisionRequest>
{
    public FeeHeadRevisionRequestValidator()
    {
        Include(new FeeHeadUpdateRequestValidator());
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
