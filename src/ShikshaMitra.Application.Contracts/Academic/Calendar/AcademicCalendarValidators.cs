using FluentValidation;

namespace ShikshaMitra.Academic.Calendar;

public class CreateCalendarDayDtoValidator : AbstractValidator<CreateCalendarDayDto>
{
	public CreateCalendarDayDtoValidator()
	{
		RuleFor(x => x.Title)
			.Cascade(CascadeMode.Stop)
			.NotEmpty()
			.WithMessage("Title is required.")
			.Must(title => !string.IsNullOrWhiteSpace(title))
			.WithMessage("Title is required.")
			.Must(title => title.Trim().Length >= AcademicCalendarConsts.CalendarDayTitleMinLength)
			.WithMessage($"Title must be at least {AcademicCalendarConsts.CalendarDayTitleMinLength} characters.")
			.Must(title => title.Trim().Length <= AcademicCalendarConsts.CalendarDayTitleMaxLength)
			.WithMessage($"Title cannot exceed {AcademicCalendarConsts.CalendarDayTitleMaxLength} characters.");

		RuleFor(x => x.Description)
			.MaximumLength(AcademicCalendarConsts.CalendarDayDescriptionMaxLength)
			.WithMessage(
				$"Description cannot exceed {AcademicCalendarConsts.CalendarDayDescriptionMaxLength} characters.")
			.When(x => x.Description != null);
	}
}

public class CreateCalendarDaysBulkDtoValidator : AbstractValidator<CreateCalendarDaysBulkDto>
{
	public CreateCalendarDaysBulkDtoValidator()
	{
		RuleForEach(x => x.Days).SetValidator(new CreateCalendarDayDtoValidator());
	}
}