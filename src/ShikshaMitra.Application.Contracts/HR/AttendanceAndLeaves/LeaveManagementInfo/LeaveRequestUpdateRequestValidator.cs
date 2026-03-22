using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public class LeaveRequestUpdateRequestValidator : AbstractValidator<LeaveRequestUpdateRequest>
{
	private static readonly string[] ValidStatuses = { "Pending", "Reviewed", "Approved", "Rejected" };

	public LeaveRequestUpdateRequestValidator()
	{
		RuleFor(x => x.Status)
			.NotEmpty().WithMessage("Status is required.")
			.Must(s => System.Array.Exists(ValidStatuses, v => v.Equals(s, System.StringComparison.OrdinalIgnoreCase)))
			.WithMessage("Status must be one of: Pending, Reviewed, Approved, Rejected.");

		RuleFor(x => x.Remarks)
			.NotEmpty()
			.When(x => x.Status != null && x.Status.Equals("Rejected", System.StringComparison.OrdinalIgnoreCase))
			.WithMessage("Remarks are required when rejecting a leave request.");
	}
}
