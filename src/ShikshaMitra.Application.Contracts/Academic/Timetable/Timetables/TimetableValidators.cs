using FluentValidation;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class SaveTimetableDtoValidator : AbstractValidator<SaveTimetableDto>
{
    public SaveTimetableDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Timetable)
            .NotEmpty()
            .WithMessage("Timetable must contain at least one day.");
    }
}

public class AssignTeachersDtoValidator : AbstractValidator<AssignTeachersDto>
{
    public AssignTeachersDtoValidator()
    {
        RuleFor(x => x.StandardId).NotEmpty();
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.Day).NotEmpty();
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty();
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.Subjects).NotEmpty()
            .WithMessage("At least one subject assignment is required.");

        RuleForEach(x => x.Subjects).ChildRules(s =>
        {
            s.RuleFor(x => x.SubjectId).NotEmpty();
            s.RuleFor(x => x.TeacherId).NotEmpty();
        });
    }
}

public class AssignRoomsDtoValidator : AbstractValidator<AssignRoomsDto>
{
    public AssignRoomsDtoValidator()
    {
        RuleFor(x => x.StandardId).NotEmpty();
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.Day).NotEmpty();
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty();
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.Subjects).NotEmpty()
            .WithMessage("At least one room assignment is required.");

        RuleForEach(x => x.Subjects).ChildRules(s =>
        {
            s.RuleFor(x => x.SubjectId).NotEmpty();
            s.RuleFor(x => x.RoomId).NotEmpty();
        });
    }
}
