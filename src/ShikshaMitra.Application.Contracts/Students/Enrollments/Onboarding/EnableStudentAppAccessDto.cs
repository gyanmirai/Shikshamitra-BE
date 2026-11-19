using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class EnableStudentAppAccessDto
{
	public string Reason { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class EnableStudentAppAccessDtoValidator : AbstractValidator<EnableStudentAppAccessDto>
{
	public EnableStudentAppAccessDtoValidator()
	{
		RuleFor(x => x.Reason).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 3)
			.WithMessage("Explain the case-specific Student access correction.").MaximumLength(400);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
	}
}
