using FluentValidation;
using ShikshaMitra.Finance.Fees.Charges.Dtos;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeAudienceValidator : AbstractValidator<ChargeAudienceDto>
{
	public ChargeAudienceValidator()
	{
		RuleFor(x => x.Scope).NotNull().SetValidator(new AcademicScopeValidator());
		RuleFor(x => x.Selection).NotNull().SetValidator(new StudentSelectionValidator());
	}
}

public class AcademicScopeValidator : AbstractValidator<AcademicScopeDto>
{
	public AcademicScopeValidator()
	{
		RuleFor(x => x.BoardId).NotEmpty();
		RuleFor(x => x.EducationMediumId).NotEmpty();
		RuleFor(x => x.ClassIds).NotEmpty().WithMessage("At least one class must be selected.");
		RuleFor(x => x.DivisionIds)
			.Must((dto, divisionIds) => divisionIds is not { Count: > 0 } || dto.ClassIds.Count == 1)
			.WithMessage("Division filters are only allowed when exactly one class is selected.");
	}
}

public class StudentSelectionValidator : AbstractValidator<StudentSelectionDto>
{
	public StudentSelectionValidator()
	{
		RuleFor(x => x.Mode).IsInEnum();
		RuleFor(x => x.StudentIds)
			.NotEmpty()
			.When(x => x.Mode == StudentSelectionMode.Explicit)
			.WithMessage("StudentIds are required when selection mode is Explicit.");
	}
}
