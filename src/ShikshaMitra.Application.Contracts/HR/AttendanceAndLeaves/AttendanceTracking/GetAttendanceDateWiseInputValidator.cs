using FluentValidation;
using System;
using System.Globalization;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;

public class GetAttendanceDateWiseInputValidator : AbstractValidator<GetAttendanceDateWiseInput>
{
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
		if (string.IsNullOrWhiteSpace(dateString))
		{
			return false;
		}

		if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
		{
			return true;
		}

		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
		{
			return true;
		}

		return DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
	}
}
