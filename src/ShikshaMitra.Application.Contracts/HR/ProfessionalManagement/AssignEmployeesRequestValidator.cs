using FluentValidation;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class AssignEmployeesRequestValidator : AbstractValidator<AssignEmployeesRequest>
{
	public AssignEmployeesRequestValidator()
	{
		// Existing professional-management workflow owns eligibility and assignment errors.
	}
}
