using FluentValidation;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

public class EmployeeAdditionalDetailsPatchDtoValidator : AbstractValidator<EmployeeAdditionalDetailsPatchDto>
{
	public EmployeeAdditionalDetailsPatchDtoValidator()
	{
		RuleFor(x => x.BplCardNo).MaximumLength(50);
		RuleFor(x => x.SecondLanguage).MaximumLength(50);
	}
}
