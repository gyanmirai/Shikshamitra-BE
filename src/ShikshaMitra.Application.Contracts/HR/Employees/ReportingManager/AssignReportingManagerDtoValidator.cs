using FluentValidation;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class AssignReportingManagerDtoValidator : AbstractValidator<AssignReportingManagerDto>
{
	public AssignReportingManagerDtoValidator()
	{
		RuleFor(x => x.ReportingManagerId).NotEmpty();
		RuleFor(x => x.ReportingManagerKind).Must(kind => kind is "Employee" or "TenantAdministrator");
	}
}
