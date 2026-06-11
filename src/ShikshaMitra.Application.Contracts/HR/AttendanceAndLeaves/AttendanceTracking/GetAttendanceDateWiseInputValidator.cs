using FluentValidation;
using System;
using System.Globalization;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;

public class GetAttendanceDateWiseInputValidator : AbstractValidator<GetAttendanceDateWiseInput>
{
	private static readonly string[] SupportedDateFormats =
		["dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"];

	public GetAttendanceDateWiseInputValidator()
	{
		RuleFor(x => x.AttendanceDate)
			.NotEmpty()
			.Must(BeValidDate)
			.WithMessage("AttendanceDate must be a valid date.");

		RuleFor(x => x.EducationMediumStageId).NotEmpty();

		RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue);

		RuleFor(x => x.PageSize).InclusiveBetween(10, 100);
	}

	private static bool BeValidDate(string dateString)
	{
		return !string.IsNullOrWhiteSpace(dateString) &&
		       DateTime.TryParseExact(
			       dateString,
			       SupportedDateFormats,
			       CultureInfo.InvariantCulture,
			       DateTimeStyles.None,
			       out _);
	}
}
