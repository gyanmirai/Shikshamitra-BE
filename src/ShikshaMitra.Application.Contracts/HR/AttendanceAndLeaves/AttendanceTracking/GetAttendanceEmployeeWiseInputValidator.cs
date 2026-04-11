using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;

public class GetAttendanceEmployeeWiseInputValidator : AbstractValidator<GetAttendanceEmployeeWiseInput>
{
	public GetAttendanceEmployeeWiseInputValidator()
	{
		RuleFor(x => x.EmployeeId).NotEmpty();

		RuleFor(x => x.StartDate).NotEmpty();

		RuleFor(x => x.EndDate).NotEmpty();
	}
}
