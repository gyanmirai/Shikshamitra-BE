using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdvanceAdmissionWorkflowDtoValidator : AbstractValidator<AdvanceAdmissionWorkflowDto>
{
	public AdvanceAdmissionWorkflowDtoValidator()
	{
		RuleFor(input => input.ExpectedCurrentStageInstanceId).NotEmpty();
		RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleFor(input => input.Reason).MaximumLength(512);
		RuleFor(input => input.Reason)
			.NotEmpty()
			.When(input => input.SkipCurrentStage || input.AdminOverride)
			.WithMessage("A reason is required when skipping or overriding workflow routing.");
	}
}

public class UpdateAdmissionApplicationFormValuesDtoValidator : AbstractValidator<UpdateAdmissionApplicationFormValuesDto>
{
	public UpdateAdmissionApplicationFormValuesDtoValidator()
	{
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleForEach(input => input.Values).ChildRules(value =>
		{
			value.RuleFor(item => item.FieldCode).NotEmpty().MaximumLength(128);
		});
	}
}

public class AdmissionApplicationDecisionDtoValidator : AbstractValidator<AdmissionApplicationDecisionDto>
{
	public AdmissionApplicationDecisionDtoValidator()
	{
		RuleFor(input => input.ConcurrencyStamp).NotEmpty();
		RuleFor(input => input.Reason).MaximumLength(512);
	}
}
