using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class UpdateLeaveTypeDtoValidator : AbstractValidator<UpdateLeaveTypeDto>
{
	public UpdateLeaveTypeDtoValidator()
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

		RuleFor(x => x.Description)
			.MaximumLength(LeaveTypeConsts.MaxDescriptionLength)
			.When(x => x.Description != null);
	}
}
