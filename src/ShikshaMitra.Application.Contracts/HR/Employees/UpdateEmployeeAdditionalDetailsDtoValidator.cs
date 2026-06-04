using FluentValidation;

namespace ShikshaMitra.HR.Employees;

public class UpdateEmployeeAdditionalDetailsDtoValidator : AbstractValidator<UpdateEmployeeAdditionalDetailsDto>
{
	public UpdateEmployeeAdditionalDetailsDtoValidator()
	{
		RuleFor(x => x.BplCardNo)
			.MaximumLength(50)
			.WithMessage("BPL Card No cannot exceed 50 characters");

		RuleFor(x => x.SecondLanguage)
			.MaximumLength(50)
			.WithMessage("Second Language cannot exceed 50 characters");
	}
}
