using System;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class StudentOnboardingWorklistValidator_Tests
{
	[Fact]
	public void Onboarding_worklist_validator_accepts_empty_filters_and_complete_supported_filters()
	{
		var validator = new StudentOnboardingListInputValidator();
		validator.Validate(new StudentOnboardingListInput()).IsValid.ShouldBeTrue();
		validator.Validate(new StudentOnboardingListInput
		{
			StudentNameFilter = "Alice Patel", StudentNameOperator = "equals", OwnerNameFilter = "Mira", OwnerNameOperator = "startsWith",
			RollNumberMin = 1, RollNumberMax = 4, RollNumberOperator = "between", Sorting = "AssignedToUserName DESC"
		}).IsValid.ShouldBeTrue();
		validator.Validate(new StudentOnboardingListInput { RollNumberFilter = 3, RollNumberOperator = "lessThan", Sorting = "Placement" }).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Onboarding_worklist_validator_rejects_incomplete_conflicting_or_unbounded_filters_with_field_feedback()
	{
		var validator = new StudentOnboardingListInputValidator();
		foreach (var input in new[]
		{
			new StudentOnboardingListInput { StudentNameFilter = new string('x', 301) },
			new StudentOnboardingListInput { OwnerNameFilter = new string('x', 301) },
			new StudentOnboardingListInput { StudentNameOperator = "or" },
			new StudentOnboardingListInput { OwnerNameOperator = "or" },
			new StudentOnboardingListInput { RollNumberFilter = 1, RollNumberOperator = "or" },
			new StudentOnboardingListInput { RollNumberOperator = "between" },
			new StudentOnboardingListInput { RollNumberMin = 1, RollNumberOperator = "between" },
			new StudentOnboardingListInput { RollNumberMin = 4, RollNumberMax = 1, RollNumberOperator = "between" },
			new StudentOnboardingListInput { RollNumberFilter = 2, RollNumberMin = 1, RollNumberMax = 4, RollNumberOperator = "between" },
			new StudentOnboardingListInput { Sorting = new string('x', 101) },
			new StudentOnboardingListInput { Sorting = "StudentName desc, Id asc" },
			new StudentOnboardingListInput { PlacementDivisionId = Guid.Empty }
		})
		{
			var result = validator.Validate(input); result.IsValid.ShouldBeFalse();
			result.Errors.ShouldAllBe(error => error.ErrorCode == StudentOnboardingErrorCodes.PolicyScopeInvalid);
		}
	}
}
