using FluentValidation;

namespace ShikshaMitra.HR.Employees.Status;

public class EmployeeStatusUpdateRequestValidator : AbstractValidator<EmployeeStatusUpdateRequest>
{
	public EmployeeStatusUpdateRequestValidator()
	{
		RuleFor(x => x.EmployeeStatusType).InclusiveBetween(0, 3);
		RuleFor(x => x.Remarks).MaximumLength(2000);
	}
}
