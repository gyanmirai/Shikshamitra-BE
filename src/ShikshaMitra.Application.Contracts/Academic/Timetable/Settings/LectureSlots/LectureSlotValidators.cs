using FluentValidation;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class CreateLectureSlotDtoValidator : AbstractValidator<CreateLectureSlotDto>
{
    public CreateLectureSlotDtoValidator()
    {
        RuleFor(x => x.StandardId)
            .NotEmpty();

        RuleFor(x => x.SchoolHours)
            .NotEmpty()
            .MaximumLength(TimetableConsts.MaxSchoolHoursLength);

        RuleFor(x => x.SlotConfigurations)
            .NotEmpty()
            .WithMessage("At least one slot configuration is required.");

        RuleForEach(x => x.SlotConfigurations).ChildRules(config =>
        {
            config.RuleFor(c => c.TotalSlots)
                .InclusiveBetween(1, TimetableConsts.MaxTotalSlots);

            config.RuleFor(c => c.WeekRange)
                .NotEmpty()
                .Must(w => w.Count == 2)
                .WithMessage("WeekRange must contain exactly 2 values (start and end).");

            config.RuleFor(c => c.TimeSlots)
                .NotEmpty()
                .WithMessage("At least one time slot is required.");
        });
    }
}

public class UpdateLectureSlotDtoValidator : AbstractValidator<UpdateLectureSlotDto>
{
    public UpdateLectureSlotDtoValidator()
    {
        RuleFor(x => x.StandardId)
            .NotEmpty();

        RuleFor(x => x.SchoolHours)
            .NotEmpty()
            .MaximumLength(TimetableConsts.MaxSchoolHoursLength);

        RuleFor(x => x.SlotConfigurations)
            .NotEmpty()
            .WithMessage("At least one slot configuration is required.");

        RuleForEach(x => x.SlotConfigurations).ChildRules(config =>
        {
            config.RuleFor(c => c.TotalSlots)
                .InclusiveBetween(1, TimetableConsts.MaxTotalSlots);

            config.RuleFor(c => c.WeekRange)
                .NotEmpty()
                .Must(w => w.Count == 2)
                .WithMessage("WeekRange must contain exactly 2 values (start and end).");

            config.RuleFor(c => c.TimeSlots)
                .NotEmpty()
                .WithMessage("At least one time slot is required.");
        });
    }
}

public class AssignLectureSlotDtoValidator : AbstractValidator<AssignLectureSlotDto>
{
    public AssignLectureSlotDtoValidator()
    {
        RuleFor(x => x.SelectedStandardIds)
            .NotEmpty()
            .WithMessage("At least one standard must be selected.");
    }
}
