using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class SaveAdmissionIntakeProfileDtoValidator : AbstractValidator<SaveAdmissionIntakeProfileDto>
{
	public SaveAdmissionIntakeProfileDtoValidator()
	{
		RuleFor(item => item.Kind).IsInEnum();
		RuleFor(item => item.Code).NotEmpty().MaximumLength(64)
			.Matches("^[A-Za-z][A-Za-z0-9_-]*$");
		RuleFor(item => item.Name).NotEmpty().MaximumLength(128);
		RuleFor(item => item.ConfigurationJson).NotEmpty().MaximumLength(8192);
	}
}
