using System;
using FluentValidation;

namespace ShikshaMitra.Students.Attendance;

public sealed class SaveAttendanceSettingsDtoValidator : AbstractValidator<SaveAttendanceSettingsDto>
{
	public SaveAttendanceSettingsDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).Must(x => !x.HasValue || x != Guid.Empty);
		RuleFor(x => x.ConcurrencyStamp).MaximumLength(40);
		RuleFor(x => x.Policy).NotNull();
	}
}
public sealed class PublishAttendanceSettingsDtoValidator : AbstractValidator<PublishAttendanceSettingsDto>
{
	public PublishAttendanceSettingsDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).Must(x => !x.HasValue || x != Guid.Empty);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(x => x.EffectiveFrom).Must(x => x != default && x.TimeOfDay == TimeSpan.Zero);
	}
}
public sealed class SaveAttendanceScheduleDtoValidator : AbstractValidator<SaveAttendanceScheduleDto>
{
	public SaveAttendanceScheduleDtoValidator()
	{
		RuleFor(x => x.Id).Must(x => !x.HasValue || x != Guid.Empty);
		RuleFor(x => x.EnrollmentId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
		RuleFor(x => x.Kind).Must(x => x is "Fixed" or "Temporary");
		RuleFor(x => x.StartDate).Must(x => x != default && x.TimeOfDay == TimeSpan.Zero);
		RuleFor(x => x.EndDate).Must(x => !x.HasValue || x.Value.TimeOfDay == TimeSpan.Zero);
		RuleFor(x => x.WeekDays).NotNull().Must(x => x != null && x.Length <= 7);
		RuleFor(x => x.StartTime).MaximumLength(5);
		RuleFor(x => x.EndTime).MaximumLength(5);
		RuleFor(x => x.ConcurrencyStamp).MaximumLength(40);
	}
}
