using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class CreateUpdateAdmissionWorkflowDtoValidator : AbstractValidator<CreateUpdateAdmissionWorkflowDto>
{
	public CreateUpdateAdmissionWorkflowDtoValidator()
	{
		RuleFor(input => input.Name)
			.NotEmpty()
			.MaximumLength(AdmissionWorkflowConsts.MaxNameLength);
		RuleFor(input => input.Code)
			.NotEmpty()
			.MaximumLength(AdmissionWorkflowConsts.MaxCodeLength)
			.Matches("^[A-Za-z][A-Za-z0-9_-]*$")
			.WithMessage("Workflow code must start with a letter and contain only letters, numbers, underscores, or hyphens.");
		RuleFor(input => input.Description)
			.MaximumLength(AdmissionWorkflowConsts.MaxDescriptionLength);
		RuleForEach(input => input.Stages)
			.SetValidator(new CreateUpdateAdmissionWorkflowStageDtoValidator());
		RuleForEach(input => input.Transitions)
			.SetValidator(new CreateUpdateAdmissionWorkflowTransitionDtoValidator());
	}
}

public class CreateUpdateAdmissionWorkflowStageDtoValidator : AbstractValidator<CreateUpdateAdmissionWorkflowStageDto>
{
	public CreateUpdateAdmissionWorkflowStageDtoValidator()
	{
		RuleFor(stage => stage.Code)
			.NotEmpty()
			.MaximumLength(AdmissionWorkflowConsts.MaxCodeLength)
			.Matches("^[A-Za-z][A-Za-z0-9_-]*$");
		RuleFor(stage => stage.Name)
			.NotEmpty()
			.MaximumLength(AdmissionWorkflowConsts.MaxNameLength);
		RuleFor(stage => stage.Description)
			.MaximumLength(AdmissionWorkflowConsts.MaxStageDescriptionLength);
		RuleFor(stage => stage.DisplayOrder)
			.GreaterThanOrEqualTo(0);
		RuleFor(stage => stage.ConfigurationJson)
			.MaximumLength(AdmissionWorkflowConsts.MaxConfigurationLength);
		RuleFor(stage => stage.StageType)
			.IsInEnum();
	}
}

public class CreateUpdateAdmissionWorkflowTransitionDtoValidator : AbstractValidator<CreateUpdateAdmissionWorkflowTransitionDto>
{
	public CreateUpdateAdmissionWorkflowTransitionDtoValidator()
	{
		RuleFor(transition => transition.FromStageId)
			.NotEmpty();
		RuleFor(transition => transition.ToStageId)
			.NotEmpty();
		RuleFor(transition => transition.Priority)
			.GreaterThanOrEqualTo(0);
		RuleFor(transition => transition.ConditionRuleJson)
			.MaximumLength(AdmissionWorkflowConsts.MaxConditionRuleLength);
	}
}

public class CloneAdmissionWorkflowDtoValidator : AbstractValidator<CloneAdmissionWorkflowDto>
{
	public CloneAdmissionWorkflowDtoValidator()
	{
		RuleFor(input => input.Name)
			.NotEmpty()
			.MaximumLength(AdmissionWorkflowConsts.MaxNameLength);
		RuleFor(input => input.Code)
			.NotEmpty()
			.MaximumLength(AdmissionWorkflowConsts.MaxCodeLength)
			.Matches("^[A-Za-z][A-Za-z0-9_]*$");
		RuleFor(input => input.Description)
			.MaximumLength(AdmissionWorkflowConsts.MaxDescriptionLength);
	}
}
