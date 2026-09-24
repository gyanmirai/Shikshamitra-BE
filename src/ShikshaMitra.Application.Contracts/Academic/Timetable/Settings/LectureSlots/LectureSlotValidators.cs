using System.Collections.Generic;
using System.Linq;
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
                .Must((slot, range) => slot.ActiveDays.Count > 0 || range.Count == 2)
                .WithMessage("WeekRange must contain exactly 2 values when ActiveDays is not provided.");

            config.RuleFor(c => c.ActiveDays)
                .Must(HaveValidActiveDays)
                .WithMessage("ActiveDays must contain unique values between Monday (0) and Saturday (5).");

            config.RuleFor(c => c.TimeSlots)
                .NotEmpty()
                .WithMessage("At least one time slot is required.");
        });
    }

    private static bool HaveValidActiveDays(List<int> days) => ActiveDaysValidator.IsValid(days);
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
                .Must((slot, range) => slot.ActiveDays.Count > 0 || range.Count == 2)
                .WithMessage("WeekRange must contain exactly 2 values when ActiveDays is not provided.");

            config.RuleFor(c => c.ActiveDays)
                .Must(HaveValidActiveDays)
                .WithMessage("ActiveDays must contain unique values between Monday (0) and Saturday (5).");

            config.RuleFor(c => c.TimeSlots)
                .NotEmpty()
                .WithMessage("At least one time slot is required.");
        });
    }

    private static bool HaveValidActiveDays(List<int> days) => ActiveDaysValidator.IsValid(days);
}

internal static class ActiveDaysValidator
{
    internal static bool IsValid(List<int> days) =>
        days.Count == 0 ||
        (days.Count <= TimetableConsts.MaxWeekRangeValue - TimetableConsts.MinWeekRangeValue + 1 &&
         days.Distinct().Count() == days.Count &&
         days.All(day => day >= TimetableConsts.MinWeekRangeValue && day <= TimetableConsts.MaxWeekRangeValue));
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
