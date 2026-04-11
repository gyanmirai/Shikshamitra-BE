using FluentValidation;

namespace ShikshaMitra.Students.Enrollments.EntranceExams;

public class ScheduleEntranceExamDtoValidator : AbstractValidator<ScheduleEntranceExamDto>
{
	public ScheduleEntranceExamDtoValidator()
	{
		RuleFor(x => x.EntranceExamSlotId)
			.NotEmpty();
	}
}
