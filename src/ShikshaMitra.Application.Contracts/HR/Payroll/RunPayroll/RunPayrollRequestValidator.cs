using FluentValidation;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class RunPayrollRequestValidator : AbstractValidator<RunPayrollRequest>
{
	public RunPayrollRequestValidator()
	{
		RuleFor(x => x.Month)
			.NotEmpty()
			.Matches(@"^[A-Z]+-\d{4}$");
	}
}
