using FluentValidation;
using System;
using ShikshaMitra.HR.Payroll.Templates;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class SalaryComponentDtoValidator : AbstractValidator<SalaryComponentDto>
{
	public SalaryComponentDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MinimumLength(2)
			.MaximumLength(100);

		RuleFor(x => x.Amount)
			.GreaterThanOrEqualTo(0m)
			.When(x => x.Amount.HasValue);

		RuleFor(x => x.ApplicableMonth)
			.Must(x => !x.HasValue || x.Value != default)
			.WithMessage("ApplicableMonth must be a valid date when provided.");

		RuleFor(x => x.SemanticType)
			.NotEmpty()
			.Must(value => PayrollComponentSemanticTypeCodes.TryParse(value, out _))
			.WithMessage("SemanticType is invalid.");
	}
}
