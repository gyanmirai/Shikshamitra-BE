using FluentValidation;
using System;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class RunPayrollRequestValidator : AbstractValidator<RunPayrollRequest>
{
	private static readonly string[] AcceptedFormats =
	[
		"yyyy-MM",
		"yyyy/M",
		"yyyy/MM",
		"MMMM-yyyy",
		"MMMM yyyy",
		"MMM-yyyy",
		"MMM yyyy",
		"dd-MM-yyyy",
		"d-M-yyyy",
		"yyyy-MM-dd",
		"dd/MM/yyyy",
		"d/M/yyyy"
	];

	public RunPayrollRequestValidator()
	{
		RuleFor(x => x.Month)
			.NotEmpty()
			.Must(BeValidMonthInput)
			.WithMessage("Month must be in YYYY-MM, MMMM-YYYY, or a common date-like format.");
	}

	private static bool BeValidMonthInput(string month)
	{
		if (string.IsNullOrWhiteSpace(month))
		{
			return false;
		}

		var trimmed = month.Trim();
		if (DateTime.TryParseExact(trimmed, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _))
		{
			return true;
		}

		return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _);
	}
}
