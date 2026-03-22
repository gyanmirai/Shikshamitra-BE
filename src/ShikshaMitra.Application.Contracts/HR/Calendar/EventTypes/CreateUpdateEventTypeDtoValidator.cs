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

		RuleFor(x => x.Color).NotEmpty();

		RuleFor(x => x.Description)
			.MaximumLength(500)
			.When(x => x.Description != null);
	}
}
