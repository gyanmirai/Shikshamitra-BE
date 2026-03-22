using FluentValidation;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;

namespace ShikshaMitra.HR.Calendar.Events;

public class CreateUpdateEventDtoValidator : AbstractValidator<CreateUpdateEventDto>
{
	public CreateUpdateEventDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty().WithMessage("Academic year ID is required.");
		RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
		RuleFor(x => x.EventTypeId).NotEmpty().WithMessage("Event type ID is required.");
		RuleFor(x => x.Description).MaximumLength(500);
		RuleFor(x => x.StartTime).NotEmpty().Matches(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$")
			.WithMessage("Start time must be in HH:mm format.");
		RuleFor(x => x.EndTime).NotEmpty().Matches(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$")
			.WithMessage("End time must be in HH:mm format.");
	}
}
