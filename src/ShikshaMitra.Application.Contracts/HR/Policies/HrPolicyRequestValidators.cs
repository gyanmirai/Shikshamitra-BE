using FluentValidation;

namespace ShikshaMitra.HR.Policies;

public class CreateUpdateWorkCalendarPolicyDtoValidator : AbstractValidator<CreateUpdateWorkCalendarPolicyDto>
{
	public CreateUpdateWorkCalendarPolicyDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class CreateUpdatePayrollPolicyDtoValidator : AbstractValidator<CreateUpdatePayrollPolicyDto>
{
	public CreateUpdatePayrollPolicyDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class CreateUpdateHrPolicyAssignmentDtoValidator : AbstractValidator<CreateUpdateHrPolicyAssignmentDto>
{
	public CreateUpdateHrPolicyAssignmentDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class UpsertEmployeePolicyOverrideDtoValidator : AbstractValidator<UpsertEmployeePolicyOverrideDto>
{
	public UpsertEmployeePolicyOverrideDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class HrPolicyCloneRequestDtoValidator : AbstractValidator<HrPolicyCloneRequestDto>
{
	public HrPolicyCloneRequestDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class HrPolicyImpactPreviewRequestDtoValidator : AbstractValidator<HrPolicyImpactPreviewRequestDto>
{
	public HrPolicyImpactPreviewRequestDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class HrPolicyAssignmentImpactPreviewRequestDtoValidator : AbstractValidator<HrPolicyAssignmentImpactPreviewRequestDto>
{
	public HrPolicyAssignmentImpactPreviewRequestDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}

public class HrPolicyRestoreDraftRequestDtoValidator : AbstractValidator<HrPolicyRestoreDraftRequestDto>
{
	public HrPolicyRestoreDraftRequestDtoValidator()
	{
		// Existing policy workflows own business invariant errors; this keeps the request in the FluentValidation pipeline.
	}
}
