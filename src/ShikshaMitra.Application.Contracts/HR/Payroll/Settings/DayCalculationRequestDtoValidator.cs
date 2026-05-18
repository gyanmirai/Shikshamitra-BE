using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationRequestDtoValidator : AbstractValidator<DayCalculationRequestDto>
{
	private static readonly string[] ValidTypes = { "working-days", "total-month-days", "manual-days" };
	private static readonly string[] ValidSaturdayPolicies = { "full-day", "half-day", "off" };
	private static readonly string[] ValidHolidaySyncModes = { "synced-with-overrides" };

	public DayCalculationRequestDtoValidator()
	{
		RuleFor(x => x.DayCalculationType)
			.NotEmpty()
			.Must(x => ValidTypes.Contains((x ?? string.Empty).Trim().ToLowerInvariant()))
			.WithMessage("dayCalculationType must be one of: working-days, total-month-days, manual-days.");

		RuleFor(x => x.SaturdayWorkPolicy)
			.Must(x => string.IsNullOrWhiteSpace(x) || ValidSaturdayPolicies.Contains(x.Trim().ToLowerInvariant()))
			.WithMessage("saturdayWorkPolicy must be one of: full-day, half-day, off.");

		RuleFor(x => x.HolidaySyncMode)
			.Must(x => string.IsNullOrWhiteSpace(x) || ValidHolidaySyncModes.Contains(x.Trim().ToLowerInvariant()))
			.WithMessage("holidaySyncMode must be synced-with-overrides.");

		RuleFor(x => x.ManualDays)
			.NotNull()
			.InclusiveBetween(1, 31)
			.When(x => (x.DayCalculationType ?? string.Empty).Trim().ToLowerInvariant() == "manual-days" && (x.ManualMonthDays == null || x.ManualMonthDays.Count == 0))
			.WithMessage("manualDays must be between 1 and 31 when dayCalculationType is manual-days.");

		RuleForEach(x => x.ManualMonthDays)
			.ChildRules(month =>
			{
				month.RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
				month.RuleFor(x => x.Month).InclusiveBetween(1, 12);
				month.RuleFor(x => x.ManualDays).InclusiveBetween(1, 31);
			});

		RuleForEach(x => x.HolidayOverrides)
			.ChildRules(holiday =>
			{
				holiday.RuleFor(x => x.Date).NotEmpty();
				holiday.RuleFor(x => x.HolidayName).MaximumLength(200);
			});
	}
}
