using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProvidentFundDtoValidator : AbstractValidator<CreateUpdateProvidentFundDto>
{
	public CreateUpdateProvidentFundDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MinimumLength(2)
			.MaximumLength(100);

		RuleFor(x => x.Code)
			.NotEmpty()
			.MinimumLength(2)
			.MaximumLength(20)
			.Matches(@"^[A-Z_]+$");

		RuleFor(x => x.EmployeePercentage).InclusiveBetween(0m, 100m);

		RuleFor(x => x.EmployerPercentage).InclusiveBetween(0m, 100m);
	}
}
