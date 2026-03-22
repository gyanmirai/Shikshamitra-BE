using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateEarningComponentDtoValidator : AbstractValidator<CreateEarningComponentDto>
{
	public CreateEarningComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(EarningComponentConsts.MaxNameLength);

		RuleFor(x => x.Description)
			.MaximumLength(EarningComponentConsts.MaxDescriptionLength)
			.When(x => x.Description != null);

		RuleFor(x => x.ComponentType).IsInEnum();
		RuleFor(x => x.DependOn).IsInEnum();

		RuleFor(x => x.Amount)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(EarningComponentConsts.MaxAmountLength);
	}
}
