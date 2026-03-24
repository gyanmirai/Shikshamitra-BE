using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProfessionalTaxDtoValidator : AbstractValidator<CreateUpdateProfessionalTaxDto>
{
	public CreateUpdateProfessionalTaxDtoValidator()
	{
		RuleFor(x => x.GenderType).IsInEnum();
	}
}
