using FluentValidation;

namespace ShikshaMitra.HR.Employees.Payroll;

public class UpdateEmployeePayrollDetailsDtoValidator : AbstractValidator<UpdateEmployeePayrollDetailsDto>
{
	public UpdateEmployeePayrollDetailsDtoValidator()
	{
		RuleFor(x => x.Earnings).NotEmpty();
		RuleFor(x => x.Deductions).NotEmpty();
	}
}
