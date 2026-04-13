using FluentValidation;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public class CreateWeeklyLectureSlotDtoValidator : AbstractValidator<CreateWeeklyLectureSlotDto>
{
    public CreateWeeklyLectureSlotDtoValidator()
    {
        RuleFor(x => x.StandardId)
            .NotEmpty();

        RuleFor(x => x.Subjects)
            .NotEmpty()
            .WithMessage("At least one subject is required.");

        RuleForEach(x => x.Subjects).ChildRules(subject =>
        {
            subject.RuleFor(s => s.WeeklyLectures)
                .InclusiveBetween(TimetableConsts.MinWeeklyLectures, TimetableConsts.MaxWeeklyLectures);
        });
    }
}

public class UpdateWeeklyLectureSlotDtoValidator : AbstractValidator<UpdateWeeklyLectureSlotDto>
{
    public UpdateWeeklyLectureSlotDtoValidator()
    {
        RuleFor(x => x.Subjects)
            .NotEmpty()
            .WithMessage("At least one subject is required.");

        RuleForEach(x => x.Subjects).ChildRules(subject =>
        {
            subject.RuleFor(s => s.WeeklyLectures)
                .InclusiveBetween(TimetableConsts.MinWeeklyLectures, TimetableConsts.MaxWeeklyLectures);
        });
    }
}
