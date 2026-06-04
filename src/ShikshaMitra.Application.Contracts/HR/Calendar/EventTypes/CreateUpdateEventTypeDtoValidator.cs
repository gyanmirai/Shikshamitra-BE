using FluentValidation;

namespace ShikshaMitra.HR.Calendar.EventTypes;

public class CreateUpdateEventTypeDtoValidator : AbstractValidator<CreateUpdateEventTypeDto>
{
	public CreateUpdateEventTypeDtoValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(256);

		RuleFor(x => x.Color)
			.NotEmpty()
			.Matches(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
			.WithMessage("Color must be a valid hex code in format #RRGGBB or #RGB");

		RuleFor(x => x.Description)
			.MaximumLength(500)
			.When(x => x.Description != null);
	}
}
