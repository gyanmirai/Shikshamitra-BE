using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdateProfessionalTaxDtoValidator : AbstractValidator<CreateUpdateProfessionalTaxDto>
{
	public CreateUpdateProfessionalTaxDtoValidator()
	{
		RuleFor(x => x.JurisdictionCode).Equal("IN-GJ");
		RuleFor(x => x.EffectiveFrom).NotEmpty();
		RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue);
		RuleFor(x => x.FromSalary).GreaterThanOrEqualTo(0);
		RuleFor(x => x.ToSalary).GreaterThanOrEqualTo(x => x.FromSalary).When(x => x.ToSalary.HasValue);
		RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
	}
}
