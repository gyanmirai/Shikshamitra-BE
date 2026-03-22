using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class UpdateDeductionComponentDtoValidator : AbstractValidator<UpdateDeductionComponentDto>
{
	public UpdateDeductionComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(DeductionComponentConsts.MaxNameLength);

		RuleFor(x => x.Description)
			.MaximumLength(DeductionComponentConsts.MaxDescriptionLength)
			.When(x => x.Description != null);

		RuleFor(x => x.ComponentType).IsInEnum();
		RuleFor(x => x.DependOn).IsInEnum();

		RuleFor(x => x.Amount)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(DeductionComponentConsts.MaxAmountLength);
	}
}
