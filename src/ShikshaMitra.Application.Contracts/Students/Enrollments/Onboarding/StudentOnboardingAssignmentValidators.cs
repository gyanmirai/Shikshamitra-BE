using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class AssignStudentOnboardingGrNumberDtoValidator : AbstractValidator<AssignStudentOnboardingGrNumberDto>
{
	public AssignStudentOnboardingGrNumberDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.NumberSeriesId).NotEmpty();
		RuleFor(x => x.ManualValue).MaximumLength(100);
		RuleFor(x => x.Reason).MaximumLength(2000);
		RuleFor(x => x.ManualSequence).GreaterThanOrEqualTo(0).When(x => x.ManualSequence.HasValue);
	}
}

public class AssignStudentOnboardingRollNumberDtoValidator : AbstractValidator<AssignStudentOnboardingRollNumberDto>
{
	public AssignStudentOnboardingRollNumberDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.RollNumber).GreaterThan(0);
		RuleFor(x => x.RollAssignmentMode).Must(x => x is null or "manual" or "automatic");
		RuleFor(x => x.Reason).MaximumLength(2000);
	}
}

public class SetStudentIdentifierLockDtoValidator : AbstractValidator<SetStudentIdentifierLockDto>
{
	public SetStudentIdentifierLockDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.DivisionId).NotEmpty();
		RuleFor(x => x.IdentifierType).Must(x => x is "gr" or "roll");
		RuleFor(x => x.Reason).MaximumLength(2000).NotEmpty().When(x => !x.IsLocked);
	}
}

public class SaveStudentOnboardingFeeStructureDtoValidator : AbstractValidator<SaveStudentOnboardingFeeStructureDto>
{
	public SaveStudentOnboardingFeeStructureDtoValidator()
	{
		RuleFor(x => x.ConcurrencyStamp).NotEmpty();
		RuleFor(x => x.StructureId).NotEmpty();
		RuleFor(x => x.OptionalHeadIds).NotNull().Must(x => x != null && x.Count <= 100);
	}
}
