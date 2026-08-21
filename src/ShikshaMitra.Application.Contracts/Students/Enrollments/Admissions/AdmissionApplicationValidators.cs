using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdvanceAdmissionWorkflowDtoValidator : AbstractValidator<AdvanceAdmissionWorkflowDto>
{
	public AdvanceAdmissionWorkflowDtoValidator()
	{
		RuleFor(input => input.ExpectedCurrentStageInstanceId).NotEmpty();
		RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(input => input.Reason).MaximumLength(512);
		RuleFor(input => input.Reason)
			.NotEmpty()
			.When(input => input.SkipCurrentStage || input.AdminOverride)
			.WithMessage("A reason is required when skipping or overriding workflow routing.");
	}
}
