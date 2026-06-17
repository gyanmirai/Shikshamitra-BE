using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class UpdateLeavePolicyDtoValidator : AbstractValidator<UpdateLeavePolicyDto>
{
	public UpdateLeavePolicyDtoValidator()
	{
		RuleFor(x => x.PolicyName)
			.NotEmpty()
			.MinimumLength(1)
			.MaximumLength(LeavePolicyConsts.MaxPolicyNameLength);

		RuleFor(x => x.MaximumDaysPerYear)
			.InclusiveBetween(LeavePolicyConsts.MinLeaveCount, LeavePolicyConsts.MaxLeaveCount);

		RuleFor(x => x.PaidType).IsInEnum();

		RuleFor(x => x.Description)
			.MaximumLength(LeavePolicyConsts.MaxDescriptionLength)
			.When(x => x.Description != null);
	}
}
