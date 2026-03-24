using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class CreateUpdatePayElementDtoValidator : AbstractValidator<CreateUpdatePayElementDto>
{
	public CreateUpdatePayElementDtoValidator()
	{
		RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
		RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
		RuleFor(x => x.Type).IsInEnum();
	}
}
