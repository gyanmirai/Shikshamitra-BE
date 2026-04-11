using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;

public class GetAttendanceDateWiseInputValidator : AbstractValidator<GetAttendanceDateWiseInput>
{
	public GetAttendanceDateWiseInputValidator()
	{
		RuleFor(x => x.AttendanceDate).NotEmpty();

		RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue);

		RuleFor(x => x.PageSize).InclusiveBetween(10, 100);
	}
}
