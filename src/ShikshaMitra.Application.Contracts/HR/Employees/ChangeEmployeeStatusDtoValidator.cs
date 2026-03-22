using FluentValidation;

namespace ShikshaMitra.HR.Employees;

public class ChangeEmployeeStatusDtoValidator : AbstractValidator<ChangeEmployeeStatusDto>
{
	public ChangeEmployeeStatusDtoValidator()
	{
		RuleFor(x => x.EmployeeStatusType).IsInEnum();
	}
}
