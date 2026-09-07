using System.Linq;
using FluentValidation;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadItemRequestValidator : AbstractValidator<FeeHeadItemRequest>
{
    public FeeHeadItemRequestValidator()
    {
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

public class FeeCategoryCreateRequestValidator : AbstractValidator<FeeCategoryCreateRequest>
{
    public FeeCategoryCreateRequestValidator()
    {
        RuleFor(x => x.Name)
			.Cascade(CascadeMode.Stop)
            .NotEmpty()
			.Must(name => FeeSetupNameNormalizer.ForStorage(name).Length is >= 3 and <= 100)
			.WithMessage("Fee-category name must contain between 3 and 100 characters after whitespace is normalized.")
			.Must(FeeSetupNameNormalizer.HasSensibleCharacters)
			.WithMessage("Fee-category name contains unsupported characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.FeeHeads)
			.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("At least one fee head is required.")
			.Must(heads => heads.Select(h => FeeSetupNameNormalizer.ForComparison(h?.Name)).Distinct().Count() == heads.Count)
            .WithMessage("Duplicate fee head names are not allowed.");

        RuleForEach(x => x.FeeHeads).SetValidator(new FeeHeadItemRequestValidator());
    }
}

public class FeeCategoryUpdateRequestValidator : AbstractValidator<FeeCategoryUpdateRequest>
{
    public FeeCategoryUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
			.Cascade(CascadeMode.Stop)
            .NotEmpty()
			.Must(name => FeeSetupNameNormalizer.ForStorage(name).Length is >= 3 and <= 100)
			.WithMessage("Fee-category name must contain between 3 and 100 characters after whitespace is normalized.")
			.Must(FeeSetupNameNormalizer.HasSensibleCharacters)
			.WithMessage("Fee-category name contains unsupported characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

public class FeeCategoryStatusUpdateRequestValidator : AbstractValidator<FeeCategoryStatusUpdateRequest>
{
    public FeeCategoryStatusUpdateRequestValidator()
    {
        RuleFor(x => x.DeactivationReason)
            .NotEmpty().When(x => !x.IsActive)
            .MaximumLength(500);
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

public class FeeCategoryCopyPreviewRequestValidator : AbstractValidator<FeeCategoryCopyPreviewRequest>
{
    public FeeCategoryCopyPreviewRequestValidator()
    {
        RuleFor(x => x.SourceAcademicYearId).NotEmpty();
        RuleFor(x => x.DestinationAcademicYearId).NotEmpty().NotEqual(x => x.SourceAcademicYearId);
        RuleFor(x => x.CategoryIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Category identifiers must be unique.");
    }
}

public class FeeCategoryCopyCommitRequestValidator : AbstractValidator<FeeCategoryCopyCommitRequest>
{
    public FeeCategoryCopyCommitRequestValidator()
    {
        Include(new FeeCategoryCopyPreviewRequestValidator());
        RuleFor(x => x.Resolutions).Must(items => items.Select(item => item.SourceCategoryId).Distinct().Count() == items.Count)
            .WithMessage("Copy resolutions must be unique per category.");
        RuleForEach(x => x.Resolutions).ChildRules(item =>
        {
            item.RuleFor(x => x.SourceCategoryId).NotEmpty();
            item.RuleFor(x => x.Action).Must(action => new[] { "Copy", "Skip", "Keep" }.Contains(action, System.StringComparer.OrdinalIgnoreCase));
			item.RuleFor(x => x.NewName)
				.Must(name => string.IsNullOrWhiteSpace(name) ||
					(FeeSetupNameNormalizer.ForStorage(name).Length is >= 3 and <= 100 && FeeSetupNameNormalizer.HasSensibleCharacters(name)))
				.WithMessage("A replacement name must contain 3 to 100 supported characters after whitespace is normalized.");
        });
    }
}
