using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateEarningComponentDtoValidator : AbstractValidator<CreateEarningComponentDto>
{
	public CreateEarningComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(EarningComponentConsts.MaxNameLength);

		RuleFor(x => x.Description)
			.MaximumLength(EarningComponentConsts.MaxDescriptionLength)
			.When(x => x.Description != null);

		RuleFor(x => x.ComponentType).IsInEnum();
		RuleFor(x => x.CalculationType).IsInEnum();
		RuleFor(x => x.DependOn).IsInEnum();

		RuleFor(x => x.Amount)
			.NotNull()
			.GreaterThanOrEqualTo(0)
			.When(x => x.CalculationType == Settings.CalculationType.Amount)
			.WithMessage("Amount is required when calculationType is AMOUNT.");

		RuleFor(x => x.Percentage)
			.NotNull()
			.InclusiveBetween(0, 100)
			.When(x => x.CalculationType == Settings.CalculationType.Percentage)
			.WithMessage("Percentage must be between 0 and 100 when calculationType is PERCENTAGE.");

		RuleFor(x => x.SelectedEarningComponentId)
			.NotNull()
			.When(x => x.CalculationType == Settings.CalculationType.Percentage)
			.WithMessage("selectedEarningComponentId is required when calculationType is PERCENTAGE.");
	}
}
