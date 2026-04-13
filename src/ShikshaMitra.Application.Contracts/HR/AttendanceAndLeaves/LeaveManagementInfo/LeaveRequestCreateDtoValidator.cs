using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public class LeaveRequestCreateDtoValidator : AbstractValidator<LeaveRequestCreateDto>
{
	public LeaveRequestCreateDtoValidator()
	{
		RuleFor(x => x.LeaveTypeId).NotEmpty().WithMessage("Leave type is required.");
		RuleFor(x => x.StartDate).NotEmpty().WithMessage("Start date is required.");
		RuleFor(x => x.EndDate).NotEmpty().WithMessage("End date is required.");

		RuleFor(x => x.EndDate)
			.GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("End date must be on or after start date.");

		RuleFor(x => x.Reason)
			.NotEmpty()
			.MinimumLength(5)
			.MaximumLength(500)
			.WithMessage("Reason is required and must be between 5 and 500 characters.");
	}
}
