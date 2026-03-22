using FluentValidation;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class SalaryComponentDtoValidator : AbstractValidator<SalaryComponentDto>
{
	public SalaryComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MinimumLength(2)
			.MaximumLength(100);

		RuleFor(x => x.Description).MaximumLength(255);

		RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);

		RuleFor(x => x.ComponentType).IsInEnum();
	}
}
