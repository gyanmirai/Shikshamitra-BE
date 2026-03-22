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

		RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000)
			.WithMessage("Reason is required and must not exceed 1000 characters.");
	}
}
