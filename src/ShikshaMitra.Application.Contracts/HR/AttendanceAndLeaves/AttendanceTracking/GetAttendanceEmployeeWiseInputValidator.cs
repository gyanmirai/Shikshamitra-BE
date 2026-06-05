using FluentValidation;
using System;
using System.Globalization;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;

public class GetAttendanceEmployeeWiseInputValidator : AbstractValidator<GetAttendanceEmployeeWiseInput>
{
	public GetAttendanceEmployeeWiseInputValidator()
	{
		RuleFor(x => x.EmployeeId).NotEmpty();

		RuleFor(x => x.StartDate)
			.NotEmpty()
			.Must(BeValidDate)
			.WithMessage("StartDate must be a valid date.");

		RuleFor(x => x.EndDate)
			.NotEmpty()
			.Must(BeValidDate)
			.WithMessage("EndDate must be a valid date.");

		RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue);
		RuleFor(x => x.PageSize).InclusiveBetween(10, 100);

		RuleFor(x => x)
			.Must(x => TryParseDate(x.EndDate, out var endDate) &&
					   TryParseDate(x.StartDate, out var startDate) &&
					   endDate >= startDate)
			.WithMessage("EndDate must be greater than or equal to StartDate.");

		RuleFor(x => x)
			.Must(x => TryParseDate(x.EndDate, out var endDate) &&
					   TryParseDate(x.StartDate, out var startDate) &&
					   (endDate - startDate).Days <= 90)
			.WithMessage("Date range must not exceed 90 days.");
	}

	private static bool BeValidDate(string dateString)
	{
		return TryParseDate(dateString, out _);
	}

	private static bool TryParseDate(string dateString, out DateTime result)
	{
		result = default;
		if (string.IsNullOrWhiteSpace(dateString))
		{
			return false;
		}

		if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return true;
		}

		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result))
		{
			return true;
		}

		if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
		{
			result = dto.DateTime;
			return true;
		}

		return false;
	}
}
