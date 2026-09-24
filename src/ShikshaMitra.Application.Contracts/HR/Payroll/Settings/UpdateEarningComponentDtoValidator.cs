using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class UpdateEarningComponentDtoValidator : AbstractValidator<UpdateEarningComponentDto>
{
	public UpdateEarningComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage("Name must not be empty or whitespace.")
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
			.NotEmpty()
			.When(x => x.CalculationType == Settings.CalculationType.Percentage && x.DependOn != ComponentDependency.Gross)
			.WithMessage("Select a component for a component-based percentage.");

		RuleFor(x => x.SelectedEarningComponentId)
			.Null()
			.When(x => x.CalculationType == Settings.CalculationType.Percentage && x.DependOn == ComponentDependency.Gross)
			.WithMessage("Gross Salary cannot reference a component.");
	}
}
