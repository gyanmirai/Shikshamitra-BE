using System;
using FluentValidation;

namespace ShikshaMitra.Students.StudentRecords;

public sealed class SaveStudentRecordsSettingsDtoValidator : AbstractValidator<SaveStudentRecordsSettingsDto>
{
	public SaveStudentRecordsSettingsDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).Must(x => !x.HasValue || x.Value != Guid.Empty);
		RuleFor(x => x.ConcurrencyStamp).MaximumLength(40);
		RuleFor(x => x.Policy).NotNull();
	}
}

public sealed class PublishStudentRecordsSettingsDtoValidator : AbstractValidator<PublishStudentRecordsSettingsDto>
{
	public PublishStudentRecordsSettingsDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).Must(x => !x.HasValue || x.Value != Guid.Empty);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
	}
}
