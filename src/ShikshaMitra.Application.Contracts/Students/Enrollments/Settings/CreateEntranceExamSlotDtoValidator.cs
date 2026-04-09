using FluentValidation;
using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class CreateEntranceExamSlotDtoValidator : AbstractValidator<CreateEntranceExamSlotDto>
{
	public CreateEntranceExamSlotDtoValidator()
	{
		RuleFor(x => x.AcademicYearId)
			.NotEmpty();

		RuleFor(x => x.SlotName)
			.MaximumLength(100)
			.When(x => x.SlotName != null);

		RuleFor(x => x.StartTimeDate)
			.NotEqual(DateTimeOffset.MinValue);

		RuleFor(x => x.EndTimeDate)
			.NotEqual(DateTimeOffset.MinValue)
			.GreaterThan(x => x.StartTimeDate)
			.WithMessage("End time must be after start time.");

		RuleFor(x => x.StudentLimit)
			.InclusiveBetween(1, 1000);

		RuleFor(x => x.RoomNo)
			.InclusiveBetween(1, 99999);
	}
}
