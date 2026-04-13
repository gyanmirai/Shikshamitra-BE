using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public class LeaveApprovalRequestValidator : AbstractValidator<LeaveApprovalRequestDto>
{
	public LeaveApprovalRequestValidator()
	{
		RuleFor(x => x.Remarks)
			.MaximumLength(2000)
			.When(x => x.Remarks != null);
	}
}

public class LeaveRejectionRequestValidator : AbstractValidator<LeaveRejectionRequestDto>
{
	public LeaveRejectionRequestValidator()
	{
		RuleFor(x => x.Remarks)
			.NotEmpty()
			.WithMessage("Remarks are required when rejecting a leave request.")
			.MaximumLength(2000);
	}
}
