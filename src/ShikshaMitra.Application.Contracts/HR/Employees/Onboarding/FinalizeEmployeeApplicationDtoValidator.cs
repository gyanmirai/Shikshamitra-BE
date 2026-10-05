using System;
using FluentValidation;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class FinalizeEmployeeApplicationDtoValidator : AbstractValidator<FinalizeEmployeeApplicationDto>
{
	public FinalizeEmployeeApplicationDtoValidator(IClock clock)
	{
		RuleFor(x => x.PersonalInfo)
			.NotNull()
			.SetValidator(new CreateEmployeePersonalInfoDtoValidator(clock));

		RuleFor(x => x.AcademicYearId)
			.NotEmpty()
			.WithMessage("Academic year is required for a teaching staff.")
			.When(IsTeaching);

		RuleFor(x => x.AcademicScopes)
			.NotNull()
			.NotEmpty()
			.WithMessage("At least one academic scope is required for a teaching staff.")
			.Must(scopes => scopes is { Count: <= 100 })
			.WithMessage("No more than 100 academic scope rows can be assigned during onboarding.")
			.When(IsTeaching);

		RuleForEach(x => x.AcademicScopes)
			.SetValidator(new EmployeeApplicationAcademicScopeDtoValidator())
			.When(IsTeaching);

		RuleFor(x => x)
			.Must(input =>
				IsTeaching(input) ||
				(!input.AcademicYearId.HasValue && (input.AcademicScopes == null || input.AcademicScopes.Count == 0)))
			.WithMessage("Academic scopes can only be supplied for a teaching staff.")
			.WithName(nameof(FinalizeEmployeeApplicationDto.AcademicScopes));
	}

	private static bool IsTeaching(FinalizeEmployeeApplicationDto input) =>
		input.PersonalInfo?.EmployeeGroup == EmployeeGroupType.Teaching;
}

public class EmployeeApplicationAcademicScopeDtoValidator
	: AbstractValidator<EmployeeApplicationAcademicScopeDto>
{
	public EmployeeApplicationAcademicScopeDtoValidator()
	{
		RuleFor(x => x)
			.Must(scope => !HasEmptyGuid(scope))
			.WithMessage("Academic scope IDs cannot be empty GUIDs.");

		RuleFor(x => x.EducationMediumId)
			.Must((scope, _) => scope.EducationBoardId.HasValue)
			.When(x => x.EducationMediumId.HasValue)
			.WithMessage("Education medium requires an education board.");

		RuleFor(x => x.EducationMediumStageId)
			.Must((scope, _) => scope.EducationBoardId.HasValue && scope.EducationMediumId.HasValue)
			.When(x => x.EducationMediumStageId.HasValue)
			.WithMessage("Education medium stage requires an education board and medium.");

		RuleFor(x => x.StandardId)
			.Must((scope, _) =>
				scope.EducationBoardId.HasValue &&
				scope.EducationMediumId.HasValue &&
				scope.EducationMediumStageId.HasValue)
			.When(x => x.StandardId.HasValue)
			.WithMessage("Standard requires an education board, medium, and medium stage.");

		RuleFor(x => x.DivisionId)
			.Must((scope, _) =>
				scope.EducationBoardId.HasValue &&
				scope.EducationMediumId.HasValue &&
				scope.EducationMediumStageId.HasValue &&
				scope.StandardId.HasValue)
			.When(x => x.DivisionId.HasValue)
			.WithMessage("Division requires an education board, medium, medium stage, and standard.");
	}

	private static bool HasEmptyGuid(EmployeeApplicationAcademicScopeDto scope) =>
		scope.EducationBoardId == Guid.Empty ||
		scope.EducationMediumId == Guid.Empty ||
		scope.EducationMediumStageId == Guid.Empty ||
		scope.StandardId == Guid.Empty ||
		scope.DivisionId == Guid.Empty;
}
