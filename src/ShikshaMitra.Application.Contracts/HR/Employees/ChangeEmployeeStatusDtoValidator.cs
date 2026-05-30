using FluentValidation;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

public class ChangeEmployeeStatusDtoValidator : AbstractValidator<ChangeEmployeeStatusDto>
{
	public ChangeEmployeeStatusDtoValidator()
	{
		RuleFor(x => x.EmployeeStatusType).IsInEnum();
		RuleFor(x => x.Remarks).MaximumLength(2000);
		RuleFor(x => x.EffectiveDate)
			.NotNull()
			.When(x => x.EmployeeStatusType != EmployeeStatusType.Active)
			.WithMessage("Effective date is required for inactive, left, or retired status.");
	}
}
