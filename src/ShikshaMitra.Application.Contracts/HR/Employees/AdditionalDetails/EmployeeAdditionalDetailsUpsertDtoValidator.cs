using FluentValidation;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

public class EmployeeAdditionalDetailsUpsertDtoValidator : AbstractValidator<EmployeeAdditionalDetailsUpsertDto>
{
	public EmployeeAdditionalDetailsUpsertDtoValidator()
	{
		RuleFor(x => x.BplCardNo).MaximumLength(50);
		RuleFor(x => x.SecondLanguage).MaximumLength(50);
	}
}
