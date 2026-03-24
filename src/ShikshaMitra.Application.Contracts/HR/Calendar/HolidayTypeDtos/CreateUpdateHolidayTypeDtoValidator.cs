using FluentValidation;

namespace ShikshaMitra.HR.Calendar;

public class CreateUpdateHolidayTypeDtoValidator : AbstractValidator<CreateUpdateHolidayTypeDto>
{
	public CreateUpdateHolidayTypeDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(256);

		RuleFor(x => x.Color)
			.NotEmpty()
			.Matches(@"^#[0-9A-Fa-f]{6}$");

		RuleFor(x => x.Description)
			.MaximumLength(500)
			.When(x => x.Description != null);
	}
}
