using FluentValidation;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;

namespace ShikshaMitra.HR.Calendar.Holidays;

public class CreateUpdateHolidayDtoValidator : AbstractValidator<CreateUpdateHolidayDto>
{
	public CreateUpdateHolidayDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();

		RuleFor(x => x.HolidayName)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(256);

		RuleFor(x => x.HolidayTypeId).NotEmpty();

		RuleFor(x => x.StartDate).NotEmpty();

		RuleFor(x => x.EndDate).NotEmpty();
		RuleFor(x => x.EndDate)
			.GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("End date must be greater than or equal to start date");

		RuleFor(x => x.Description)
			.MaximumLength(500)
			.When(x => x.Description != null);
	}
}
