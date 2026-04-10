using FluentValidation;
using System;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class UpdateEntranceExamSlotDtoValidator : AbstractValidator<UpdateEntranceExamSlotDto>
{
	public UpdateEntranceExamSlotDtoValidator()
	{
		RuleFor(x => x.SlotName)
			.MaximumLength(100)
			.When(x => x.SlotName != null);

		RuleFor(x => x.StartDateTime)
			.NotEqual(DateTimeOffset.MinValue);

		RuleFor(x => x.EndDateTime)
			.NotEqual(DateTimeOffset.MinValue)
			.GreaterThan(x => x.StartDateTime)
			.WithMessage("End time must be after start time.");

		RuleFor(x => x.StudentLimit)
			.InclusiveBetween(1, 1000);

		RuleFor(x => x.RoomNo)
			.InclusiveBetween(1, 99999);
	}
}
