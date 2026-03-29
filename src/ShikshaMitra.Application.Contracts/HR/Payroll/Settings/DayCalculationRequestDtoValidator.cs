using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationRequestDtoValidator : AbstractValidator<DayCalculationRequestDto>
{
	private static readonly string[] ValidTypes = { "working-days", "total-month-days", "manual-days" };

	public DayCalculationRequestDtoValidator()
	{
		RuleFor(x => x.DayCalculationType)
			.NotEmpty()
			.Must(x => ValidTypes.Contains((x ?? string.Empty).Trim().ToLowerInvariant()))
			.WithMessage("dayCalculationType must be one of: working-days, total-month-days, manual-days.");

		RuleFor(x => x.ManualDays)
			.NotNull()
			.InclusiveBetween(1, 31)
			.When(x => (x.DayCalculationType ?? string.Empty).Trim().ToLowerInvariant() == "manual-days")
			.WithMessage("manualDays must be between 1 and 31 when dayCalculationType is manual-days.");
	}
}
