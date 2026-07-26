using FluentValidation;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentImpactPreviewRequestDtoValidator :
	AbstractValidator<HrPolicyAssignmentImpactPreviewRequestDto>
{
	public HrPolicyAssignmentImpactPreviewRequestDtoValidator()
	{
		RuleFor(x => x.Action).NotEmpty().MaximumLength(50);
		RuleFor(x => x.Assignment)
			.NotNull()
			.SetValidator(new CreateUpdateHrPolicyAssignmentDtoValidator());
	}
}

public class PolicyAssignmentPreviewRequestDtoValidator :
	AbstractValidator<PolicyAssignmentPreviewRequestDto>
{
	public PolicyAssignmentPreviewRequestDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.Date)
			.Must(date => date != default)
			.WithMessage("Preview date is required.");
		RuleFor(x => x)
			.Must(x => x.EmployeeId.HasValue || x.GroupId.HasValue)
			.WithMessage("Employee or group is required for policy assignment preview.");
	}
}

public class HrPolicyRestoreDraftRequestDtoValidator :
	AbstractValidator<HrPolicyRestoreDraftRequestDto>
{
	public HrPolicyRestoreDraftRequestDtoValidator()
	{
		RuleFor(x => x.Name).MaximumLength(200);
		RuleFor(x => x.ChangeReason).MaximumLength(500);
	}
}
