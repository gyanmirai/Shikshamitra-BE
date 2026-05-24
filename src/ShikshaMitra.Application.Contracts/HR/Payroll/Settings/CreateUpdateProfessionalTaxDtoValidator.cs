using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProfessionalTaxDtoValidator : AbstractValidator<CreateUpdateProfessionalTaxDto>
{
	public CreateUpdateProfessionalTaxDtoValidator()
	{
		RuleFor(x => x.GenderType).IsInEnum();
		RuleFor(x => x.FromSalary).GreaterThanOrEqualTo(0);
		RuleFor(x => x.ToSalary).GreaterThanOrEqualTo(x => x.FromSalary);
		RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
	}
}
