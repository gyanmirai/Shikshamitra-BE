using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class CreateUpdateCustomAttendanceRuleDtoValidator : AbstractValidator<CustomAttendanceRuleDto>
{
	public CreateUpdateCustomAttendanceRuleDtoValidator()
	{
		RuleFor(x => x.Name).NotEmpty();
	}
}
