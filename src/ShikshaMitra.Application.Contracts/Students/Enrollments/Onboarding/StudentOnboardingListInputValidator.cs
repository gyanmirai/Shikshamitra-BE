using System;
using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingListInputValidator : AbstractValidator<StudentOnboardingListInput>
{
	public StudentOnboardingListInputValidator()
	{
		RuleFor(x => x.StudentNameFilter).MaximumLength(300).WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		RuleFor(x => x.OwnerNameFilter).MaximumLength(300).WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		RuleFor(x => x.StudentNameOperator).Must(BeTextOperator)
			.WithMessage("Choose a supported student name filter.").WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		RuleFor(x => x.OwnerNameOperator).Must(BeTextOperator)
			.WithMessage("Choose a supported owner name filter.").WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		RuleFor(x => x.RollNumberOperator).Must(value => value is null or "equals" or "greaterThan" or "lessThan" or "between")
			.WithMessage("Choose a supported Roll number filter.").WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		RuleFor(x => x.PlacementDivisionId).NotEqual(Guid.Empty)
			.When(x => x.PlacementDivisionId.HasValue).WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		RuleFor(x => x).Must(HaveCompleteRollFilter)
			.WithMessage("Enter one Roll number, or both ends of an ordered Roll number range.")
			.WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid).OverridePropertyName(nameof(StudentOnboardingListInput.RollNumberFilter));
		RuleFor(x => x.Sorting).Cascade(CascadeMode.Stop).MaximumLength(100).WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid)
			.Must(BeSupportedSort).WithMessage("Choose a supported ascending or descending worklist sort.")
			.WithErrorCode(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	private static bool BeTextOperator(string? value) => value is null or "contains" or "equals" or "startsWith" or "endsWith";

	private static bool HaveCompleteRollFilter(StudentOnboardingListInput input)
	{
		var hasValues = input.RollNumberFilter.HasValue || input.RollNumberMin.HasValue || input.RollNumberMax.HasValue;
		if (input.RollNumberOperator == "between")
			return !input.RollNumberFilter.HasValue && input.RollNumberMin.HasValue && input.RollNumberMax.HasValue &&
				input.RollNumberMin <= input.RollNumberMax;
		return !hasValues || input.RollNumberFilter.HasValue && !input.RollNumberMin.HasValue && !input.RollNumberMax.HasValue;
	}

	private static bool BeSupportedSort(string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting)) return true;
		var parts = sorting.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return (parts.Length is 1 or 2) && (parts[0] is "StudentName" or "OwnerName" or "AssignedToUserName" or
			"DivisionName" or "Placement" or "RollNumber" or "Status") &&
			(parts.Length == 1 || string.Equals(parts[1], "asc", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase));
	}
}
