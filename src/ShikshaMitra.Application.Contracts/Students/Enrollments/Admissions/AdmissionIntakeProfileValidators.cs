using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class SaveAdmissionIntakeProfileDtoValidator : AbstractValidator<SaveAdmissionIntakeProfileDto>
{
	public SaveAdmissionIntakeProfileDtoValidator()
	{
		RuleFor(item => item.Kind).IsInEnum();
		RuleFor(item => item.Code).MaximumLength(64);
		RuleFor(item => item.Code).Matches("^[A-Za-z][A-Za-z0-9_-]*$")
			.When(item => !string.IsNullOrWhiteSpace(item.Code));
		RuleFor(item => item.Name).NotEmpty().MaximumLength(128);
		RuleFor(item => item.ConfigurationJson).NotEmpty().MaximumLength(8192);
	}
}
