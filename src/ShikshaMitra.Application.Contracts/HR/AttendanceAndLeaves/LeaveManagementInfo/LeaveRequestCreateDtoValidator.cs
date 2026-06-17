using System;
using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public class LeaveRequestCreateDtoValidator : AbstractValidator<LeaveRequestCreateDto>
{
	public LeaveRequestCreateDtoValidator()
	{
		RuleFor(x => x.PaidType)
			.Must(value =>
				string.IsNullOrWhiteSpace(value) ||
				string.Equals(value, "Paid", StringComparison.Ordinal) ||
				string.Equals(value, "Unpaid", StringComparison.Ordinal))
			.WithMessage("Paid type must be Paid or Unpaid.");
		RuleFor(x => x.StartDate).NotEmpty().WithMessage("Start date is required.");
		RuleFor(x => x.EndDate).NotEmpty().WithMessage("End date is required.");

		RuleFor(x => x.EndDate)
			.GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("End date must be on or after start date.");

		RuleFor(x => x)
			.Must(x => !x.HalfDay || x.StartDate.Date == x.EndDate.Date)
			.WithMessage("Half-day leave must start and end on the same date.");

		RuleFor(x => x.Reason)
			.NotEmpty()
			.MinimumLength(5)
			.MaximumLength(500)
			.WithMessage("Reason is required and must be between 5 and 500 characters.");
	}
}
