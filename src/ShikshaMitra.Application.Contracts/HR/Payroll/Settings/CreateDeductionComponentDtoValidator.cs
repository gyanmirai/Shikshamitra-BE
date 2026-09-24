using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateDeductionComponentDtoValidator : AbstractValidator<CreateDeductionComponentDto>
{
	public CreateDeductionComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage("Name must not be empty or whitespace.")
			.MaximumLength(DeductionComponentConsts.MaxNameLength);

		RuleFor(x => x.Description)
			.MaximumLength(DeductionComponentConsts.MaxDescriptionLength)
			.When(x => x.Description != null);

		RuleFor(x => x.ComponentType).IsInEnum();
		RuleFor(x => x.CalculationType).IsInEnum();
		RuleFor(x => x.DependOn).IsInEnum();

		RuleFor(x => x.Amount)
			.NotNull()
			.GreaterThanOrEqualTo(0)
			.When(x =>
				x.ComponentType == DeductionComponentType.Fixed
				&& x.CalculationType == Settings.CalculationType.Amount)
			.WithMessage("Amount is required when deductionType is FIXED and calculationType is AMOUNT.");

		RuleFor(x => x.Amount)
			.GreaterThanOrEqualTo(0)
			.When(x =>
				x.Amount.HasValue
				&& !(x.ComponentType == DeductionComponentType.Fixed
				     && x.CalculationType == Settings.CalculationType.Amount))
			.WithMessage("Amount must be a non-negative number.");

		RuleFor(x => x.Percentage)
			.NotNull()
			.InclusiveBetween(0, 100)
			.When(x => x.CalculationType == Settings.CalculationType.Percentage)
			.WithMessage("Percentage must be between 0 and 100 when calculationType is PERCENTAGE.");

		RuleFor(x => x.SelectedDeductionComponentId)
			.NotEmpty()
			.When(x => x.CalculationType == Settings.CalculationType.Percentage && x.DependOn != ComponentDependency.Gross)
			.WithMessage("Select a component for a component-based percentage.");

		RuleFor(x => x.SelectedDeductionComponentId)
			.Null()
			.When(x => x.CalculationType == Settings.CalculationType.Percentage && x.DependOn == ComponentDependency.Gross)
			.WithMessage("Gross Salary cannot reference a component.");
	}
}
