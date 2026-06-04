using FluentValidation;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;

namespace ShikshaMitra.HR.Calendar.Events;

public class CreateUpdateEventDtoValidator : AbstractValidator<CreateUpdateEventDto>
{
	public CreateUpdateEventDtoValidator()
	{
		RuleFor(x => x.AcademicYearId)
			.NotEmpty()
			.WithMessage("Academic year ID is required");
		RuleFor(x => x.Title)
			.NotEmpty()
			.WithMessage("Event title is required")
			.Length(1, 256)
			.WithMessage("Event title must be between 1 and 256 characters");
		RuleFor(x => x.EventTypeId)
			.NotEmpty()
			.WithMessage("Event type ID is required");
		RuleFor(x => x.Description)
			.MaximumLength(500)
			.WithMessage("Description cannot exceed 500 characters");
		RuleFor(x => x.StartTime)
			.NotEmpty()
			.WithMessage("Start time is required")
			.Matches(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$")
			.WithMessage("Start time must be in HH:mm format");
		RuleFor(x => x.EndTime)
			.NotEmpty()
			.WithMessage("End time is required")
			.Matches(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$")
			.WithMessage("End time must be in HH:mm format");
	}
}
