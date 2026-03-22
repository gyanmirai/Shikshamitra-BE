using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class CreateLeaveTypeDtoValidator : AbstractValidator<CreateLeaveTypeDto>
{
	public CreateLeaveTypeDtoValidator()
	{
		RuleFor(x => x.Type)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(LeaveTypeConsts.MaxTypeLength);

		RuleFor(x => x.Color)
			.NotEmpty()
			.MinimumLength(7)
			.MaximumLength(LeaveTypeConsts.MaxColorLength)
			.Matches(@"^#[A-Fa-f0-9]{6}$");
	}
}
